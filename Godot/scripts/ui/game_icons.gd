class_name GameIcons
extends RefCounted
## Painted-look vector icons for skills, potions and loot, drawn from SVG at runtime and cached.
## Each icon stacks motifs (a sword, a flame, a shield, chevrons...) drawn in a 100 × 100 box:
## a soft glow in the icon's colour, a dark outline, a light fill shaded top to bottom, dark
## details on top. Every skill has its own recipe, keyed by ability id (the Unity sprite names are
## sk_<id>); anything unknown falls back to its element.

## Motif parts: ["f", path] filled light · ["s", path, width] stroked light · ["d", path, width]
## dark detail line · ["k", path] dark fill · ["h", path] bright highlight fill · ["c", path] fill
## in the icon's own colour. "star:n:outer:inner" makes a star polygon around the centre.
const MOTIFS := {
	"sword": [["f", "M50 4 L58 17 L58 66 L42 66 L42 17 Z"], ["f", "M24 66 L76 66 L80 59 L84 67 L77 75 L23 75 L16 67 L20 59 Z"],
		["f", "M45 75 L55 75 L55 89 L45 89 Z"], ["f", "M50 87 L58 95 L50 103 L42 95 Z"], ["d", "M50 21 L50 62", 3.0]],
	"dagger": [["f", "M50 12 L57 24 L57 60 L43 60 L43 24 Z"], ["f", "M31 60 L69 60 L69 68 L31 68 Z"], ["f", "M45 68 L55 68 L55 82 L45 82 Z"],
		["f", "M50 81 L56 87 L50 93 L44 87 Z"], ["d", "M50 27 L50 56", 2.5]],
	"axe": [["f", "M46 12 L54 12 L54 97 L46 97 Z"], ["f", "M54 16 C 70 8, 90 16, 94 40 C 84 37, 72 41, 66 50 C 62 45, 58 43, 54 43 Z"],
		["f", "M46 20 L30 27 L46 35 Z"], ["d", "M62 20 C 74 18, 84 24, 88 33", 2.5]],
	"hammer": [["f", "M46 34 L54 34 L54 97 L46 97 Z"], ["f", "M16 10 L84 10 C 88 10, 90 13, 90 17 L90 36 C 90 40, 88 42, 84 42 L16 42 C 12 42, 10 40, 10 36 L10 17 C 10 13, 12 10, 16 10 Z"],
		["d", "M24 10 L24 42 M76 10 L76 42", 3.0]],
	"arrow": [["f", "M50 3 L63 27 L53 23 L53 80 L47 80 L47 23 L37 27 Z"], ["f", "M47 72 L35 92 L35 78 L47 60 Z"], ["f", "M53 72 L65 92 L65 78 L53 60 Z"]],
	"bow": [["s", "M32 6 C 76 24, 76 76, 32 94", 8.0], ["s", "M32 8 L32 92", 2.5]],
	"shield": [["f", "M50 5 L87 17 C 87 52, 75 79, 50 97 C 25 79, 13 52, 13 17 Z"], ["d", "M50 18 L50 84 M23 33 L77 33", 3.0],
		["c", "M50 40 L60 50 L50 62 L40 50 Z"]],
	"heart": [["f", "M50 90 C 18 68, 6 48, 16 28 C 26 10, 44 14, 50 30 C 56 14, 74 10, 84 28 C 94 48, 82 68, 50 90 Z"],
		["h", "M26 30 C 30 22, 38 22, 42 28 C 36 28, 30 32, 28 38 Z"]],
	"plus": [["f", "M41 18 L59 18 L59 41 L82 41 L82 59 L59 59 L59 82 L41 82 L41 59 L18 59 L18 41 L41 41 Z"]],
	"flame": [["f", "M50 3 C 61 22, 83 32, 81 60 C 80 82, 66 97, 50 97 C 34 97, 19 84, 19 62 C 19 46, 30 36, 36 21 C 40 33, 44 40, 48 42 C 46 28, 45 15, 50 3 Z"],
		["h", "M50 50 C 59 61, 65 68, 63 80 C 61 90, 39 90, 37 80 C 35 70, 43 62, 50 50 Z"]],
	"snowflake": [["s", "M50 6 L50 94 M12 28 L88 72 M12 72 L88 28", 7.0],
		["s", "M50 21 L40 11 M50 21 L60 11 M50 79 L40 89 M50 79 L60 89 M25 36 L12 39 M25 36 L22 23 M75 64 L88 61 M75 64 L78 77 M25 64 L12 61 M25 64 L22 77 M75 36 L88 39 M75 36 L78 23", 5.0]],
	"bolt": [["f", "M61 2 L19 56 L46 56 L35 98 L83 38 L55 38 L69 2 Z"]],
	"skull": [["f", "M50 8 C 76 8, 88 26, 88 46 C 88 60, 80 68, 74 71 L74 86 L26 86 L26 71 C 20 68, 12 60, 12 46 C 12 26, 24 8, 50 8 Z"],
		["k", "M25 48 C 25 41, 31 37, 37 38 C 44 39, 47 45, 45 52 C 43 58, 36 60, 31 58 C 27 56, 25 52, 25 48 Z M75 48 C 75 41, 69 37, 63 38 C 56 39, 53 45, 55 52 C 57 58, 64 60, 69 58 C 73 56, 75 52, 75 48 Z M50 60 L44 71 L56 71 Z"],
		["d", "M38 78 L38 86 M50 78 L50 86 M62 78 L62 86", 3.0]],
	"burst": [["f", "star:8:48:18"], ["h", "star:8:24:10"]],
	"star": [["f", "star:5:46:19"]],
	"sparkle": [["f", "star:4:46:11"]],
	"moon": [["f", "M64 6 C 36 10, 18 30, 18 54 C 18 78, 38 96, 62 96 C 78 96, 88 88, 94 78 C 84 82, 64 82, 52 70 C 38 56, 38 26, 64 6 Z"]],
	"sun": [["f", "star:12:48:30"], ["h", "M50 28 A 22 22 0 1 0 50.01 28 Z"]],
	"eye": [["f", "M5 50 C 25 21, 75 21, 95 50 C 75 79, 25 79, 5 50 Z"], ["k", "M50 33 A 17 17 0 1 0 50.01 33 Z"],
		["c", "M50 42 A 8 8 0 1 0 50.01 42 Z"], ["h", "M43 38 A 4 4 0 1 0 43.01 38 Z"]],
	"hand": [["f", "M26 48 L74 48 L74 72 C 74 86, 63 96, 50 96 C 35 96, 26 85, 26 72 Z"],
		["s", "M32 54 L32 23 M44 50 L44 13 M56 50 L56 15 M68 56 L68 28", 11.0], ["s", "M29 72 L14 54", 11.0]],
	"fist": [["f", "M20 36 C 20 27, 28 22, 36 24 L70 24 C 80 24, 86 30, 86 40 L86 66 C 86 80, 74 92, 58 92 L40 92 C 28 92, 20 82, 20 70 Z"],
		["d", "M38 24 L38 46 M53 24 L53 46 M68 24 L68 46 M20 52 C 32 52, 44 54, 50 62", 3.0]],
	"wing": [["f", "M8 72 C 18 32, 50 10, 94 8 C 86 21, 79 25, 71 29 C 81 31, 86 35, 88 40 C 76 44, 67 46, 59 48 C 67 52, 71 56, 73 61 C 57 63, 45 63, 35 67 C 41 73, 43 77, 43 82 C 30 82, 17 78, 8 72 Z"],
		["d", "M20 66 C 40 44, 60 30, 86 14", 2.5]],
	"boot": [["f", "M30 6 L60 6 L60 56 C 72 58, 88 64, 92 77 L92 92 L20 92 L20 77 C 24 71, 30 66, 30 60 Z"], ["d", "M30 22 L60 22 M20 80 L92 80", 3.0]],
	"whirl": [["s", "M80 42 A 32 32 0 0 0 28 26", 9.0], ["s", "M20 58 A 32 32 0 0 0 72 74", 9.0],
		["f", "M14 22 L38 16 L32 40 Z"], ["f", "M86 78 L62 84 L68 60 Z"]],
	"note": [["f", "M22 76 C 22 66, 34 60, 44 62 C 52 64, 54 72, 50 80 C 46 88, 34 92, 26 88 C 23 86, 22 81, 22 76 Z"], ["s", "M49 72 L49 10", 6.0],
		["f", "M49 8 C 62 14, 80 20, 78 42 C 73 32, 62 29, 49 29 Z"]],
	"leaf": [["f", "M50 5 C 83 26, 87 66, 50 95 C 13 66, 17 26, 50 5 Z"], ["d", "M50 16 L50 88 M50 40 L36 30 M50 40 L64 30 M50 60 L33 49 M50 60 L67 49", 3.0]],
	"vine": [["s", "M8 84 C 30 62, 18 40, 40 30 C 60 20, 70 42, 92 16", 8.0],
		["f", "M24 64 L10 58 L20 52 Z M38 30 L34 16 L46 23 Z M66 33 L76 44 L62 42 Z M85 22 L94 32 L80 31 Z M18 78 L8 72 L16 68 Z"]],
	"tentacle": [["f", "M20 96 C 18 70, 30 56, 48 50 C 66 44, 72 30, 62 18 C 58 12, 49 12, 49 20 C 49 27, 58 27, 59 21 C 68 35, 58 47, 44 53 C 25 61, 30 81, 35 96 Z"],
		["k", "M30 76 A 3 3 0 1 0 30.01 76 Z M36 62 A 3 3 0 1 0 36.01 62 Z M50 55 A 3 3 0 1 0 50.01 55 Z M61 45 A 2.5 2.5 0 1 0 61.01 45 Z"]],
	"orb": [["f", "M50 18 A 32 32 0 1 0 50.01 18 Z"], ["h", "M38 30 A 10 10 0 1 0 38.01 30 Z"], ["c", "M50 36 A 14 14 0 1 0 50.01 36 Z"]],
	"comet": [["f", "M62 14 A 24 24 0 1 0 62.01 14 Z"], ["f", "M44 48 L6 94 L30 62 L14 68 L36 42 Z"], ["h", "M55 26 A 8 8 0 1 0 55.01 26 Z"]],
	"chevrons": [["s", "M14 22 L38 50 L14 78 M40 22 L64 50 L40 78 M66 22 L90 50 L66 78", 10.0]],
	"vortex": [["s", "M50 44 A 6 6 0 1 1 44 50 A 12 12 0 1 1 62 50 A 20 20 0 1 1 30 50 A 30 30 0 1 1 80 50 A 38 38 0 0 1 50 88", 6.0]],
	"slash": [["f", "M6 86 C 22 34, 62 10, 96 12 C 66 26, 40 50, 22 90 Z"]],
	"waves": [["f", "M16 50 A 8 8 0 1 0 16.01 50 Z"], ["s", "M34 30 A 26 26 0 0 1 34 70 M48 18 A 40 40 0 0 1 48 82 M62 8 A 54 54 0 0 1 62 92", 7.0]],
	"drop": [["f", "M50 5 C 64 31, 81 49, 81 64 C 81 82, 67 95, 50 95 C 33 95, 19 82, 19 64 C 19 49, 36 31, 50 5 Z"],
		["h", "M33 62 C 33 72, 38 79, 45 82 C 37 82, 30 74, 30 64 Z"]],
	"trap": [["s", "M8 72 L92 72", 6.0], ["f", "M14 72 L20 40 L28 57 L36 33 L44 57 L50 30 L56 57 L64 33 L72 57 L80 40 L86 72 Z"]],
	"cloud": [["f", "M24 70 C 8 70, 6 48, 22 45 C 21 28, 41 22, 51 33 C 57 18, 82 21, 81 42 C 96 45, 95 70, 78 70 Z"]],
	"beam": [["c", "M36 0 L64 0 L64 100 L36 100 Z"], ["f", "M43 0 L57 0 L57 100 L43 100 Z"]],
	"target": [["s", "M50 14 A 36 36 0 1 0 50.01 14 M50 32 A 18 18 0 1 0 50.01 32", 6.0], ["s", "M50 0 L50 24 M50 76 L50 100 M0 50 L24 50 M76 50 L100 50", 5.0],
		["c", "M50 44 A 6 6 0 1 0 50.01 44 Z"]],
	"person": [["f", "M50 6 A 13 13 0 1 0 50.01 6 Z"], ["f", "M24 96 C 24 60, 34 40, 50 40 C 66 40, 76 60, 76 96 Z"]],
	"smoke": [["f", "M22 78 C 8 78, 6 58, 20 54 C 18 38, 36 32, 44 42 C 48 26, 72 26, 74 44 C 90 44, 94 66, 82 72 C 88 84, 70 92, 60 84 C 52 94, 30 92, 22 78 Z"],
		["d", "M30 66 C 40 70, 52 70, 60 64", 2.5]],
	"rune": [["s", "M50 10 A 40 40 0 1 0 50.01 10", 5.0], ["s", "M50 18 L79 70 L21 70 Z", 4.0], ["c", "M50 44 A 7 7 0 1 0 50.01 44 Z"]],
	"mask": [["f", "M14 26 C 30 16, 70 16, 86 26 C 88 58, 72 92, 50 92 C 28 92, 12 58, 14 26 Z"],
		["k", "M24 42 L44 48 L40 56 L26 52 Z M76 42 L56 48 L60 56 L74 52 Z M30 68 C 40 78, 60 78, 70 68 C 62 72, 38 72, 30 68 Z"]],
	"coin": [["f", "M50 8 A 42 42 0 1 0 50.01 8 Z"], ["d", "M50 18 A 32 32 0 1 0 50.01 18", 3.0], ["k", "star:5:18:8"]],
	"flask": [["f", "M40 6 L60 6 L60 14 L56 14 L56 34 C 74 40, 84 54, 84 68 C 84 86, 68 96, 50 96 C 32 96, 16 86, 16 68 C 16 54, 26 40, 44 34 L44 14 L40 14 Z"],
		["c", "M20 66 C 30 60, 70 60, 80 66 C 80 84, 66 92, 50 92 C 34 92, 20 84, 20 66 Z"], ["h", "M27 62 C 29 52, 36 46, 42 44 C 38 50, 34 56, 33 64 Z"]],
	"gem": [["f", "M30 12 L70 12 L90 36 L50 92 L10 36 Z"], ["d", "M10 36 L90 36 M30 12 L38 36 L50 92 M70 12 L62 36 L50 92", 2.5], ["h", "M32 16 L44 16 L36 32 L18 34 Z"]],
	"bag": [["f", "M36 20 L64 20 L58 34 C 76 42, 88 58, 88 72 C 88 88, 72 96, 50 96 C 28 96, 12 88, 12 72 C 12 58, 24 42, 42 34 Z"],
		["d", "M38 34 L62 34", 4.0], ["k", "star:5:12:5"]],
	"claw": [["f", "M20 92 C 18 60, 34 26, 70 8 C 52 30, 44 58, 46 92 Z"], ["f", "M48 92 C 48 66, 60 40, 88 26 C 74 46, 68 68, 70 92 Z"]],
	"bone": [["s", "M24 76 L76 24", 12.0], ["f", "M14 70 A 10 10 0 1 0 14.01 70 Z M28 84 A 10 10 0 1 0 28.01 84 Z M72 16 A 10 10 0 1 0 72.01 16 Z M86 30 A 10 10 0 1 0 86.01 30 Z"]],
	"mushroom": [["f", "M8 52 C 8 24, 30 8, 50 8 C 70 8, 92 24, 92 52 Z"], ["f", "M38 52 L62 52 L64 86 C 64 92, 36 92, 36 86 Z"],
		["h", "M28 26 A 6 6 0 1 0 28.01 26 Z M56 18 A 5 5 0 1 0 56.01 18 Z M74 34 A 6 6 0 1 0 74.01 34 Z"]],
	"apple": [["f", "M50 28 C 64 18, 88 24, 88 52 C 88 76, 70 94, 58 92 C 54 91, 52 89, 50 89 C 48 89, 46 91, 42 92 C 30 94, 12 76, 12 52 C 12 24, 36 18, 50 28 Z"],
		["s", "M50 30 C 50 20, 52 14, 56 8", 4.0], ["c", "M56 14 C 66 6, 78 8, 82 14 C 72 20, 62 20, 56 14 Z"]],
	"feather": [["f", "M86 6 C 50 12, 22 40, 18 82 L26 90 C 64 82, 90 50, 86 6 Z"], ["d", "M80 14 L12 94", 3.0]],
	"scroll": [["f", "M22 14 L78 14 L78 86 L22 86 Z"], ["f", "M14 10 A 8 8 0 1 0 14.01 10 Z M86 10 A 8 8 0 1 0 86.01 10 Z"],
		["d", "M32 32 L68 32 M32 46 L68 46 M32 60 L60 60", 3.5]],
	"book": [["f", "M12 16 C 26 12, 40 14, 50 22 C 60 14, 74 12, 88 16 L88 86 C 74 82, 60 84, 50 92 C 40 84, 26 82, 12 86 Z"], ["d", "M50 22 L50 92", 3.0],
		["c", "M62 34 L76 32 L76 44 L62 46 Z"]],
	"ring": [["s", "M50 34 A 28 28 0 1 0 50.01 34", 9.0], ["f", "M38 8 L62 8 L70 22 L50 36 L30 22 Z"]],
	"helm": [["f", "M14 62 C 14 30, 30 12, 50 12 C 70 12, 86 30, 86 62 L86 84 L64 84 L64 60 L36 60 L36 84 L14 84 Z"], ["d", "M50 12 L50 52", 4.0]],
	"armor": [["f", "M30 10 L42 16 C 46 20, 54 20, 58 16 L70 10 L90 24 L80 44 L72 40 L72 92 L28 92 L28 40 L20 44 L10 24 Z"], ["d", "M50 22 L50 88 M34 56 L66 56", 3.0]],
	"key": [["s", "M34 34 A 18 18 0 1 0 34.01 34", 8.0], ["s", "M46 46 L88 88 M74 74 L84 64 M64 64 L72 56", 8.0]],
	"core": [["f", "star:6:46:30"], ["c", "M50 30 A 20 20 0 1 0 50.01 30 Z"], ["h", "M44 38 A 6 6 0 1 0 44.01 38 Z"]],
	"crown": [["f", "M10 80 L14 30 L32 52 L50 20 L68 52 L86 30 L90 80 Z"], ["c", "M50 56 A 7 7 0 1 0 50.01 56 Z"]],
	"web": [["s", "M50 6 L50 94 M6 50 L94 50 M18 18 L82 82 M82 18 L18 82", 3.5], ["s", "M50 22 L70 30 L78 50 L70 70 L50 78 L30 70 L22 50 L30 30 Z M50 38 L59 41 L62 50 L59 59 L50 62 L41 59 L38 50 L41 41 Z", 3.5]],
	"fang": [["f", "M30 8 L70 8 C 70 40, 62 70, 50 94 C 38 70, 30 40, 30 8 Z"], ["d", "M40 16 C 42 40, 46 60, 50 80", 2.5]],
	"lotus": [["f", "M50 20 C 62 36, 62 56, 50 72 C 38 56, 38 36, 50 20 Z M50 72 C 30 70, 16 56, 12 40 C 30 42, 42 54, 50 72 Z M50 72 C 70 70, 84 56, 88 40 C 70 42, 58 54, 50 72 Z"],
		["c", "M20 80 C 36 74, 64 74, 80 80 C 64 86, 36 86, 20 80 Z"]],
	"meat": [["f", "M30 14 C 54 6, 82 22, 84 48 C 86 70, 66 82, 48 76 C 34 72, 22 58, 20 40 C 19 28, 22 18, 30 14 Z"], ["f", "M48 76 L28 92 L20 86 L36 70 Z"],
		["c", "M38 30 C 52 26, 68 36, 70 50 C 60 44, 48 40, 38 30 Z"]],
	"bread": [["f", "M10 64 C 10 36, 30 22, 50 22 C 70 22, 90 36, 90 64 L90 72 C 90 78, 86 80, 80 80 L20 80 C 14 80, 10 78, 10 72 Z"], ["d", "M30 38 L38 54 M48 34 L54 52 M66 38 L70 54", 3.5]],
	"shell": [["f", "M10 70 C 10 36, 30 14, 50 14 C 70 14, 90 36, 90 70 C 76 82, 24 82, 10 70 Z"], ["d", "M50 14 L50 78 M30 22 L36 76 M70 22 L64 76 M12 50 L88 50", 3.0]],
	"scale": [["f", "M50 8 C 72 20, 84 44, 80 70 C 72 88, 28 88, 20 70 C 16 44, 28 20, 50 8 Z"], ["d", "M50 20 C 40 40, 40 60, 50 80", 3.0]],
	"horn": [["f", "M14 90 C 18 50, 44 20, 90 8 C 66 30, 50 56, 40 92 Z"], ["d", "M26 66 L44 72 M36 46 L54 52 M50 30 L66 36", 3.0]],
}

## Each layer: [motif, style, x, y, scale, angle (degrees)]; style "main" light with a coloured
## glow, "back" in the icon's colour, behind.
const SKILLS := {
	"slash": [["slash", "back", 50, 50, 1.0, 0], ["sword", "main", 50, 52, 0.85, 45]],
	"charge": [["chevrons", "back", 64, 50, 0.7, 0], ["shield", "main", 40, 50, 0.78, 0]],
	"whirl": [["whirl", "back", 50, 50, 1.0, 0], ["sword", "main", 50, 50, 0.72, 45]],
	"secondwind": [["whirl", "back", 50, 50, 1.0, 20], ["heart", "main", 50, 52, 0.72, 0]],
	"endure": [["shield", "back", 50, 50, 1.0, 0], ["heart", "main", 50, 50, 0.5, 0]],
	"surge": [["burst", "back", 50, 50, 1.0, 0], ["bolt", "main", 50, 50, 0.72, 0]],
	"bladestorm": [["whirl", "back", 50, 50, 1.05, 0], ["sword", "main", 38, 54, 0.6, 30], ["sword", "main", 62, 54, 0.6, -30], ["sword", "main", 50, 44, 0.62, 0]],
	"dash": [["chevrons", "main", 50, 50, 0.9, 0]],
	"anthem": [["waves", "back", 60, 50, 0.9, 0], ["note", "main", 40, 50, 0.8, 0]],
	"arcaneshield": [["rune", "back", 50, 50, 1.0, 0], ["shield", "main", 50, 52, 0.7, 0]],
	"arrow": [["arrow", "main", 50, 50, 0.95, 45]],
	"auraprotect": [["sun", "back", 50, 50, 1.0, 0], ["shield", "main", 50, 52, 0.68, 0]],
	"axethrow": [["chevrons", "back", 28, 62, 0.5, 0], ["axe", "main", 56, 46, 0.8, 30]],
	"balllightning": [["orb", "back", 50, 50, 1.0, 0], ["bolt", "main", 50, 50, 0.62, 0]],
	"barkskin": [["leaf", "back", 50, 50, 1.0, 30], ["shield", "main", 50, 52, 0.66, 0]],
	"blackhole": [["vortex", "main", 50, 50, 1.0, 0]],
	"bladefan": [["dagger", "main", 30, 56, 0.7, -35], ["dagger", "main", 70, 56, 0.7, 35], ["dagger", "main", 50, 46, 0.75, 0]],
	"blizzard": [["snowflake", "back", 62, 62, 0.6, 0], ["cloud", "main", 46, 40, 0.8, 0]],
	"chainlightning": [["bolt", "back", 66, 54, 0.75, 20], ["bolt", "main", 40, 48, 0.8, -10]],
	"chaosbolt": [["burst", "back", 50, 50, 1.0, 22], ["orb", "main", 50, 50, 0.6, 0]],
	"cleave": [["slash", "back", 50, 50, 1.0, 0], ["axe", "main", 50, 52, 0.8, 30]],
	"curse": [["rune", "back", 50, 50, 1.0, 0], ["skull", "main", 50, 52, 0.62, 0]],
	"darkarmor": [["moon", "back", 50, 50, 1.0, 0], ["shield", "main", 54, 52, 0.66, 0]],
	"deathstrike": [["skull", "back", 64, 36, 0.55, 0], ["dagger", "main", 44, 56, 0.85, 40]],
	"disengage": [["chevrons", "back", 34, 50, 0.7, 180], ["boot", "main", 62, 50, 0.7, 0]],
	"drain": [["vortex", "back", 50, 50, 1.0, 0], ["drop", "main", 50, 52, 0.66, 0]],
	"eclipse": [["sun", "back", 50, 50, 1.0, 0], ["moon", "main", 50, 50, 0.72, 0]],
	"eldritch": [["rune", "back", 50, 50, 1.0, 0], ["eye", "main", 50, 50, 0.7, 0]],
	"entangle": [["vine", "main", 50, 50, 1.0, 0]],
	"evasion": [["chevrons", "back", 34, 54, 0.55, 0], ["wing", "main", 54, 48, 0.85, 0]],
	"fireball": [["flame", "back", 50, 44, 1.0, 0], ["orb", "main", 50, 58, 0.6, 0]],
	"firebolt": [["flame", "back", 36, 60, 0.7, 30], ["arrow", "main", 54, 46, 0.82, 45]],
	"flurry": [["slash", "back", 40, 60, 0.8, 0], ["slash", "back", 60, 40, 0.8, 0], ["sword", "main", 50, 50, 0.78, 45]],
	"flurryblows": [["waves", "back", 64, 50, 0.8, 0], ["fist", "main", 40, 50, 0.72, 0]],
	"flyingkick": [["chevrons", "back", 30, 40, 0.55, 0], ["boot", "main", 58, 56, 0.75, -30]],
	"frostarmor": [["snowflake", "back", 50, 50, 1.0, 0], ["shield", "main", 50, 52, 0.66, 0]],
	"frostarrows": [["snowflake", "back", 72, 72, 0.45, 0], ["arrow", "main", 38, 50, 0.7, 30], ["arrow", "main", 54, 44, 0.7, 45], ["arrow", "main", 64, 34, 0.7, 60]],
	"guidingbolt": [["star", "back", 66, 34, 0.6, 0], ["comet", "main", 46, 54, 0.85, 0]],
	"heal": [["heart", "back", 50, 50, 1.0, 0], ["plus", "main", 50, 50, 0.6, 0]],
	"heavenfall": [["beam", "back", 50, 50, 1.0, 0], ["sword", "main", 50, 50, 0.85, 180]],
	"hex": [["rune", "back", 50, 50, 1.0, 0], ["moon", "main", 50, 50, 0.55, 0]],
	"holycharge": [["sun", "back", 50, 50, 1.0, 0], ["chevrons", "main", 50, 50, 0.6, 0]],
	"huntermark": [["target", "back", 50, 50, 1.0, 0], ["arrow", "main", 50, 50, 0.6, 45]],
	"hypnotic": [["vortex", "back", 50, 50, 1.0, 0], ["eye", "main", 50, 50, 0.7, 0]],
	"ice": [["snowflake", "main", 50, 50, 0.95, 0]],
	"iceage": [["burst", "back", 50, 50, 1.0, 0], ["snowflake", "main", 50, 50, 0.75, 0]],
	"iceprison": [["rune", "back", 50, 50, 1.0, 0], ["snowflake", "main", 50, 50, 0.6, 0]],
	"layonhands": [["plus", "back", 50, 40, 0.7, 0], ["hand", "main", 50, 56, 0.72, 0]],
	"lightning": [["bolt", "main", 50, 50, 0.95, 0]],
	"lightningbrand": [["bolt", "back", 50, 50, 1.0, 0], ["sword", "main", 50, 52, 0.75, 45]],
	"lightpillar": [["beam", "back", 50, 50, 1.0, 0], ["star", "main", 50, 42, 0.6, 0]],
	"meteor": [["comet", "main", 50, 50, 0.95, 0]],
	"missile": [["comet", "main", 34, 62, 0.45, 0], ["comet", "main", 58, 66, 0.45, 0], ["comet", "main", 62, 36, 0.5, 0]],
	"misty": [["smoke", "back", 50, 56, 1.0, 0], ["boot", "main", 50, 42, 0.55, 0]],
	"mockery": [["waves", "back", 70, 50, 0.7, 0], ["mask", "main", 42, 50, 0.75, 0]],
	"multishot": [["arrow", "main", 32, 56, 0.72, 20], ["arrow", "main", 50, 50, 0.72, 45], ["arrow", "main", 68, 44, 0.72, 70]],
	"notes": [["note", "main", 36, 54, 0.65, 0], ["note", "main", 64, 44, 0.65, 0]],
	"palm": [["burst", "back", 50, 40, 0.8, 0], ["hand", "main", 50, 54, 0.78, 0]],
	"parry": [["sword", "main", 38, 50, 0.75, 35], ["sword", "main", 62, 50, 0.75, -35]],
	"patience": [["rune", "back", 50, 50, 1.0, 0], ["hand", "main", 50, 54, 0.6, 0]],
	"piercing": [["target", "back", 60, 50, 0.8, 0], ["arrow", "main", 46, 50, 0.85, 90]],
	"radiance": [["sun", "main", 50, 50, 0.95, 0]],
	"rage": [["burst", "back", 50, 50, 1.0, 0], ["fist", "main", 50, 52, 0.66, 0]],
	"warcry": [["burst", "back", 36, 50, 0.7, 0], ["waves", "main", 54, 50, 0.85, 0]],
	"sacredflame": [["star", "back", 50, 50, 1.0, 0], ["flame", "main", 50, 52, 0.7, 0]],
	"shadowclone": [["person", "back", 64, 54, 0.72, 0], ["person", "main", 40, 54, 0.78, 0]],
	"shadowknives": [["moon", "back", 50, 50, 1.0, 0], ["dagger", "main", 38, 54, 0.62, -25], ["dagger", "main", 62, 54, 0.62, 25]],
	"shadowstep": [["moon", "back", 40, 44, 0.8, 0], ["boot", "main", 58, 56, 0.66, 0]],
	"shield": [["shield", "main", 50, 50, 0.95, 0]],
	"smash": [["burst", "back", 50, 62, 0.85, 0], ["hammer", "main", 50, 44, 0.75, -20]],
	"smite": [["sun", "back", 50, 50, 1.0, 0], ["hammer", "main", 50, 50, 0.66, -20]],
	"smokebomb": [["smoke", "main", 50, 52, 0.95, 0]],
	"song": [["waves", "back", 64, 50, 0.8, 0], ["note", "main", 42, 52, 0.78, 0]],
	"soulsiphon": [["vortex", "back", 50, 50, 1.0, 0], ["skull", "main", 50, 52, 0.6, 0]],
	"soundwave": [["waves", "main", 50, 50, 0.95, 0]],
	"spiritguard": [["wing", "back", 50, 46, 1.0, 0], ["shield", "main", 50, 56, 0.6, 0]],
	"stab": [["chevrons", "back", 30, 66, 0.5, 45], ["dagger", "main", 54, 46, 0.9, 45]],
	"slam": [["waves", "back", 50, 76, 0.7, 90], ["fist", "main", 50, 44, 0.7, 180]],
	"stormfield": [["rune", "back", 50, 50, 1.0, 0], ["bolt", "main", 50, 50, 0.65, 0]],
	"stunstrike": [["sparkle", "back", 70, 30, 0.5, 0], ["sparkle", "back", 30, 28, 0.38, 0], ["fist", "main", 50, 58, 0.7, 0]],
	"tentacles": [["tentacle", "main", 50, 50, 0.95, 0]],
	"thornstorm": [["whirl", "back", 50, 50, 1.0, 0], ["vine", "main", 50, 50, 0.7, 0]],
	"thornwhip": [["slash", "back", 50, 50, 0.9, 0], ["vine", "main", 50, 50, 0.85, 0]],
	"throwknife": [["chevrons", "back", 30, 66, 0.5, 45], ["dagger", "main", 56, 44, 0.75, 60]],
	"thrust": [["chevrons", "back", 26, 50, 0.55, 0], ["sword", "main", 56, 50, 0.85, 90]],
	"thunderstep": [["bolt", "back", 62, 40, 0.7, 0], ["boot", "main", 44, 56, 0.66, 0]],
	"thunderstorm": [["bolt", "back", 50, 66, 0.6, 0], ["cloud", "main", 50, 40, 0.85, 0]],
	"thunderwave": [["bolt", "back", 30, 50, 0.6, 0], ["waves", "main", 56, 50, 0.85, 0]],
	"trap": [["trap", "main", 50, 50, 0.95, 0]],
	"voidstorm": [["vortex", "back", 50, 50, 1.0, 0], ["cloud", "main", 50, 44, 0.66, 0]],
	"volley": [["arrow", "main", 30, 50, 0.6, 180], ["arrow", "main", 50, 56, 0.6, 180], ["arrow", "main", 70, 50, 0.6, 180]],
	"windstep": [["chevrons", "back", 30, 50, 0.55, 0], ["wing", "main", 58, 50, 0.75, 0]],
}

## Colour per element: [glow / own colour, deep shade of the fill].
const TINTS := {
	"blade": [Color("f2c46b"), Color("c9b48f")], "fire": [Color("ff7a2e"), Color("ffb36b")], "ice": [Color("6fdcff"), Color("a8e6ff")],
	"bolt": [Color("8fa8ff"), Color("c8d2ff")], "poison": [Color("8be04a"), Color("c6ef9a")], "holy": [Color("ffd75e"), Color("ffe9a6")],
	"dark": [Color("b07cff"), Color("cdb3ff")], "support": [Color("43e0b0"), Color("a9f2db")], "move": [Color("9fd8ff"), Color("d6ecff")],
	"gold": [Color("ffcc33"), Color("ffe08a")], "xp": [Color("7fb8ff"), Color("cfe3ff")], "kill": [Color("ff6a4a"), Color("e8d8cc")],
	"red": [Color("ff4a4a"), Color("ffb0a0")], "blue": [Color("4a8dff"), Color("b0ccff")], "green": [Color("5ee06a"), Color("c0f0b0")],
	"loot": [Color("f2c46b"), Color("e3d4b4")], "rare": [Color("6fb3ff"), Color("d0e4ff")], "epic": [Color("c07cff"), Color("e2ccff")],
}

static var _cache := {}


## The element of an ability, the same words the HUD always used.
static func element_of(a: Ability) -> String:
	if a == null:
		return ""
	if a.has_tag(Ability.TAG_MOVEMENT):
		return "move"
	if a.has_tag(Ability.TAG_SUPPORT):
		return "support"
	if a.has_tag(Ability.TAG_FIRE):
		return "fire"
	if a.has_tag(Ability.TAG_ICE):
		return "ice"
	if a.has_tag(Ability.TAG_LIGHTNING):
		return "bolt"
	if a.has_tag(Ability.TAG_POISON):
		return "poison"
	if a.has_tag(Ability.TAG_HOLY):
		return "holy"
	if a.has_tag(Ability.TAG_DARK):
		return "dark"
	return "blade"


static func tint(kind: String) -> Color:
	return TINTS.get(kind, TINTS.blade)[0]


## The icon of an ability.
static func skill(a: Ability, px := 96) -> Texture2D:
	if a == null:
		return null
	var kind := element_of(a)
	var recipe: Array = SKILLS.get(a.id, [])
	if recipe.is_empty():
		var fallback := {"fire": "flame", "ice": "snowflake", "bolt": "bolt", "poison": "drop", "holy": "sun", "dark": "moon",
			"support": "heart", "move": "chevrons"}
		recipe = [[fallback.get(kind, "sword"), "main", 50, 50, 0.9, 45 if kind == "blade" else 0]]
	return icon("skill:" + a.id, recipe, kind, px)


## Loot and log icons: "coin", "xp", "kill", "level", or an item id.
static func item(id: String, px := 64) -> Texture2D:
	var r := _item_recipe(id)
	return icon("item:" + id, r[0], r[1], px)


static func _item_recipe(id: String) -> Array:
	var one := func(m: String, kind: String, angle := 0.0) -> Array:
		return [[[m, "main", 50, 50, 0.92, angle]], kind]
	match id:
		"coin", "gold":
			return [[["coin", "main", 40, 56, 0.66, 0], ["coin", "main", 60, 44, 0.66, 0]], "gold"] if id == "gold" else one.call("coin", "gold")
		"xp":
			return [[["sparkle", "back", 68, 30, 0.45, 0], ["star", "main", 46, 54, 0.8, 0]], "xp"]
		"kill":
			return one.call("skull", "kill")
		"level":
			return [[["burst", "back", 50, 50, 1.0, 0], ["star", "main", 50, 50, 0.66, 0]], "gold"]
		"potion_red":
			return one.call("flask", "red")
		"potion_blue":
			return one.call("flask", "blue")
		"potion_green":
			return one.call("flask", "green")
		"apple":
			return one.call("apple", "red")
		"meat":
			return one.call("meat", "red")
		"bread":
			return one.call("bread", "loot")
		"gel", "crystal_jelly", "mud_core":
			return one.call("drop", "green" if id == "gel" else "rare")
		"herb":
			return one.call("leaf", "green")
		"lotus":
			return one.call("lotus", "support")
		"shroom_cap":
			return one.call("mushroom", "red")
		"claw":
			return one.call("claw", "loot")
		"bone":
			return one.call("bone", "loot")
		"key":
			return one.call("key", "gold", 0)
		"scroll":
			return one.call("scroll", "loot")
		"ring", "ring_eye", "ring_mimic":
			return one.call("ring", "gold")
		"sword", "blackwind_blade":
			return one.call("sword", "loot", 45)
		"crown_toad", "toad_crown":
			return one.call("crown", "gold")
		"spider_silk", "crystal_silk":
			return one.call("web", "rare")
	if id.begins_with("tome_"):
		var a := Ability.get_by_id(id.trim_prefix("tome_"))
		return [[["book", "main", 50, 50, 0.92, 0]], element_of(a) if a else "epic"]
	if id.begins_with("gem_"):
		return one.call("gem", "red" if id.ends_with("red") else "blue")
	if id.contains("core") or id.contains("essence") or id.contains("shard"):
		return one.call("core", "rare")
	if id.begins_with("shield"):
		return one.call("shield", "loot")
	if id.begins_with("helm") or id.begins_with("scarf"):
		return one.call("helm", "loot")
	if id.begins_with("armor"):
		return one.call("armor", "loot")
	if id.begins_with("boots"):
		return one.call("boot", "loot")
	if id.contains("fang") or id.contains("tooth"):
		return one.call("fang", "loot")
	if id.contains("wing") or id.contains("feather"):
		return one.call("feather", "loot")
	if id.contains("horn"):
		return one.call("horn", "loot")
	if id.contains("scale") or id.contains("skin") or id.contains("hide") or id.contains("pelt"):
		return one.call("scale", "loot")
	if id.contains("shell") or id.contains("plate"):
		return one.call("shell", "loot")
	if id.contains("eye") or id.contains("lens"):
		return one.call("eye", "rare")
	if id.contains("venom") or id.contains("gland") or id.contains("poison"):
		return one.call("drop", "poison")
	return one.call("bag", "loot")


## Renders (once) the layered motifs to a texture `px` pixels square.
static func icon(key: String, recipe: Array, kind: String, px: int) -> Texture2D:
	var ck := "%s@%d" % [key, px]
	if _cache.has(ck):
		return _cache[ck]
	var img := Image.new()
	var err := img.load_svg_from_string(svg(recipe, kind), px / 128.0)
	if err != OK:
		push_warning("GameIcons: could not draw %s" % key)
		return null
	var tex := ImageTexture.create_from_image(img)
	_cache[ck] = tex
	return tex


static func svg(recipe: Array, kind: String) -> String:
	var t: Array = TINTS.get(kind, TINTS.blade)
	var glow: Color = t[0]
	var shade: Color = t[1]
	var out := PackedStringArray()
	out.append('<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">')
	out.append('<defs><linearGradient id="fill" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fffdf6"/>' +
		'<stop offset="1" stop-color="#%s"/></linearGradient>' % shade.to_html(false) +
		'<radialGradient id="halo" cx="0.5" cy="0.5" r="0.5"><stop offset="0" stop-color="#%s" stop-opacity="0.55"/>' % glow.to_html(false) +
		'<stop offset="1" stop-color="#%s" stop-opacity="0"/></radialGradient></defs>' % glow.to_html(false))
	out.append('<circle cx="64" cy="64" r="62" fill="url(#halo)"/>')
	for layer in recipe:
		var parts: Array = MOTIFS.get(layer[0], [])
		var xf := 'transform="translate(%.1f %.1f) rotate(%.1f) scale(%.3f) translate(-50 -50)"' % [
			14.0 + float(layer[2]), 14.0 + float(layer[3]), float(layer[5]), float(layer[4])]
		var col := "#" + glow.to_html(false)
		out.append('<g %s stroke-linejoin="round" stroke-linecap="round">' % xf)
		var passes := ["back_rim", "back"] if layer[1] == "back" else ["glow", "rim", "fill", "detail"]
		for pass_name in passes:
			for part in parts:
				out.append(_part(part, pass_name, col))
		out.append('</g>')
	out.append('</svg>')
	return "".join(out)


## One motif part in one pass: a soft glow, a dark rim, the light fill, then dark lines,
## highlights and coloured gems on top. "back" layers are flat in the icon's colour.
static func _part(p: Array, pass_name: String, col: String) -> String:
	var kind: String = p[0]
	var d := _path(p[1])
	var w: float = p[2] if p.size() > 2 else 0.0
	var shape := kind == "f" or kind == "s"
	match pass_name:
		"glow":
			if shape:
				return '<path d="%s" fill="none" stroke="%s" stroke-width="%.1f" stroke-opacity="0.35"/>' % [d, col, w + 12.0]
		"rim", "back_rim":
			var extra := 7.0 if pass_name == "rim" else 4.0
			if kind == "f":
				return '<path d="%s" fill="#120c08" stroke="#120c08" stroke-width="%.1f"/>' % [d, extra]
			if kind == "s":
				return '<path d="%s" fill="none" stroke="#120c08" stroke-width="%.1f"/>' % [d, w + extra]
		"fill":
			if kind == "f":
				return '<path d="%s" fill="url(#fill)"/>' % d
			if kind == "s":
				return '<path d="%s" fill="none" stroke="#fff6e4" stroke-width="%.1f"/>' % [d, w]
		"back":
			if kind == "f":
				return '<path d="%s" fill="%s" fill-opacity="0.8"/>' % [d, col]
			if kind == "s":
				return '<path d="%s" fill="none" stroke="%s" stroke-width="%.1f" stroke-opacity="0.8"/>' % [d, col, w]
		"detail":
			match kind:
				"d":
					return '<path d="%s" fill="none" stroke="#3a2a1e" stroke-width="%.1f" stroke-opacity="0.85"/>' % [d, w]
				"k":
					return '<path d="%s" fill="#1a120c"/>' % d
				"h":
					return '<path d="%s" fill="#ffffff" fill-opacity="0.85"/>' % d
				"c":
					return '<path d="%s" fill="%s" stroke="#120c08" stroke-width="2"/>' % [d, col]
	return ""


static func _path(d: String) -> String:
	if not d.begins_with("star:"):
		return d
	var bits := d.split(":")
	var n := int(bits[1])
	var outer := float(bits[2])
	var inner := float(bits[3])
	var pts := PackedStringArray()
	for i in n * 2:
		var a := PI * i / n - PI * 0.5
		var r := outer if i % 2 == 0 else inner
		pts.append("%.1f %.1f" % [50.0 + cos(a) * r, 50.0 + sin(a) * r])
	return "M" + " L".join(pts) + " Z"
