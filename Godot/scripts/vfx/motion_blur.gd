class_name MotionBlur
extends CompositorEffect
## Per-object motion blur (the settings' "Làm Mờ Chuyển Động"): after the scene is drawn, each
## pixel is smeared along its own motion vector (camera and moving things alike, so the hero the
## camera follows stays sharp while the world streaks past a Lướt). Two compute passes through a
## scratch texture; `strength` 0 turns it off.

const SAMPLES := 10
## Longest smear in pixels at full strength.
const MAX_PIXELS := 36.0

## Pass 1 reads the scene and its motion vectors and writes the smeared picture to a scratch
## texture (every pixel, smeared or not); pass 2 copies it back (the scene's colour buffer cannot
## be copied from directly).
const BLUR_GLSL := """
#version 450
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(set = 0, binding = 0) uniform sampler2D source_color;
layout(set = 0, binding = 1) uniform sampler2D velocity;
layout(rgba16f, set = 0, binding = 2) uniform restrict writeonly image2D out_image;
layout(push_constant, std430) uniform Params {
	vec2 size;
	float strength;
	float max_pixels;
} p;

void main() {
	ivec2 px = ivec2(gl_GlobalInvocationID.xy);
	if (px.x >= int(p.size.x) || px.y >= int(p.size.y)) {
		return;
	}
	vec2 uv = (vec2(px) + 0.5) / p.size;
	vec2 v = textureLod(velocity, uv, 0.0).xy * p.strength;
	float len = length(v * p.size);
	if (len < 0.75) {
		imageStore(out_image, px, textureLod(source_color, uv, 0.0));
		return;
	}
	if (len > p.max_pixels) {
		v *= p.max_pixels / len;
	}
	vec4 sum = vec4(0.0);
	for (int i = 0; i < %d; i++) {
		float t = float(i) / float(%d - 1) - 0.5;
		sum += textureLod(source_color, uv + v * t, 0.0);
	}
	imageStore(out_image, px, sum / float(%d));
}
""" % [SAMPLES, SAMPLES, SAMPLES]

const COPY_GLSL := """
#version 450
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(rgba16f, set = 0, binding = 0) uniform restrict readonly image2D src_image;
layout(rgba16f, set = 0, binding = 1) uniform restrict writeonly image2D dst_image;
layout(push_constant, std430) uniform Params {
	vec2 size;
	vec2 pad;
} p;

void main() {
	ivec2 px = ivec2(gl_GlobalInvocationID.xy);
	if (px.x >= int(p.size.x) || px.y >= int(p.size.y)) {
		return;
	}
	imageStore(dst_image, px, imageLoad(src_image, px));
}
"""

var strength := 0.2

var _rd: RenderingDevice
var _shader: RID
var _pipeline: RID
var _copy_shader: RID
var _copy_pipeline: RID
var _sampler: RID


func _init() -> void:
	effect_callback_type = EFFECT_CALLBACK_TYPE_POST_TRANSPARENT
	needs_motion_vectors = true
	RenderingServer.call_on_render_thread(_build)


func _notification(what: int) -> void:
	if what == NOTIFICATION_PREDELETE and _rd:
		if _sampler.is_valid():
			_rd.free_rid(_sampler)
		if _shader.is_valid():
			_rd.free_rid(_shader)
		if _copy_shader.is_valid():
			_rd.free_rid(_copy_shader)


func _build() -> void:
	_rd = RenderingServer.get_rendering_device()
	if _rd == null:
		return
	_shader = _compile(BLUR_GLSL)
	_copy_shader = _compile(COPY_GLSL)
	if not _shader.is_valid() or not _copy_shader.is_valid():
		return
	_pipeline = _rd.compute_pipeline_create(_shader)
	_copy_pipeline = _rd.compute_pipeline_create(_copy_shader)
	var ss := RDSamplerState.new()
	ss.min_filter = RenderingDevice.SAMPLER_FILTER_LINEAR
	ss.mag_filter = RenderingDevice.SAMPLER_FILTER_LINEAR
	ss.repeat_u = RenderingDevice.SAMPLER_REPEAT_MODE_CLAMP_TO_EDGE
	ss.repeat_v = RenderingDevice.SAMPLER_REPEAT_MODE_CLAMP_TO_EDGE
	_sampler = _rd.sampler_create(ss)


func _compile(code: String) -> RID:
	var src := RDShaderSource.new()
	src.language = RenderingDevice.SHADER_LANGUAGE_GLSL
	src.source_compute = code
	var spirv := _rd.shader_compile_spirv_from_source(src)
	if spirv.compile_error_compute != "":
		push_error("motion blur shader: " + spirv.compile_error_compute)
		return RID()
	return _rd.shader_create_from_spirv(spirv)


static func _uniform(type: int, binding: int, ids: Array) -> RDUniform:
	var u := RDUniform.new()
	u.uniform_type = type
	u.binding = binding
	for id in ids:
		u.add_id(id)
	return u


func _run(pipeline: RID, shader: RID, uniforms: Array, push: PackedFloat32Array, groups: Vector2i) -> void:
	var set := UniformSetCacheRD.get_cache(shader, 0, uniforms)
	var list := _rd.compute_list_begin()
	_rd.compute_list_bind_compute_pipeline(list, pipeline)
	_rd.compute_list_bind_uniform_set(list, set, 0)
	var bytes := push.to_byte_array()
	_rd.compute_list_set_push_constant(list, bytes, bytes.size())
	_rd.compute_list_dispatch(list, groups.x, groups.y, 1)
	_rd.compute_list_end()


func _render_callback(_type: int, render_data: RenderData) -> void:
	if strength <= 0.001 or _rd == null or not _pipeline.is_valid() or not _copy_pipeline.is_valid():
		return
	var buffers := render_data.get_render_scene_buffers() as RenderSceneBuffersRD
	if buffers == null:
		return
	var size := buffers.get_internal_size()
	if size.x == 0 or size.y == 0:
		return
	var color_tex := buffers.get_color_texture()
	var fmt := _rd.texture_get_format(color_tex)
	if fmt.format != RenderingDevice.DATA_FORMAT_R16G16B16A16_SFLOAT:
		return
	if not buffers.has_texture("motion_blur", "copy"):
		buffers.create_texture("motion_blur", "copy", fmt.format,
			RenderingDevice.TEXTURE_USAGE_SAMPLING_BIT | RenderingDevice.TEXTURE_USAGE_STORAGE_BIT,
			RenderingDevice.TEXTURE_SAMPLES_1, size, buffers.get_view_count(), 1, true, false)
	var push := PackedFloat32Array([size.x, size.y, strength, MAX_PIXELS * strength + 4.0])
	var push_copy := PackedFloat32Array([size.x, size.y, 0.0, 0.0])
	var groups := Vector2i(ceili(size.x / 8.0), ceili(size.y / 8.0))
	for view in buffers.get_view_count():
		var color := buffers.get_color_layer(view)
		var vel := buffers.get_velocity_layer(view)
		var scratch := buffers.get_texture_slice("motion_blur", "copy", view, 0, 1, 1)
		if not color.is_valid() or not vel.is_valid() or not scratch.is_valid():
			continue
		_run(_pipeline, _shader, [
			_uniform(RenderingDevice.UNIFORM_TYPE_SAMPLER_WITH_TEXTURE, 0, [_sampler, color]),
			_uniform(RenderingDevice.UNIFORM_TYPE_SAMPLER_WITH_TEXTURE, 1, [_sampler, vel]),
			_uniform(RenderingDevice.UNIFORM_TYPE_IMAGE, 2, [scratch])], push, groups)
		_run(_copy_pipeline, _copy_shader, [
			_uniform(RenderingDevice.UNIFORM_TYPE_IMAGE, 0, [scratch]),
			_uniform(RenderingDevice.UNIFORM_TYPE_IMAGE, 1, [color])], push_copy, groups)
