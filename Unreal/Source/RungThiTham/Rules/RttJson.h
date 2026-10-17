// A small JSON reader for the game's data files (Content/Data/*.json, written from the Unity
// assets by Tools/GodotData/unity_to_json.py). Plain C++ so the rules load their numbers the same
// way in the game, in a dedicated server and in Tools/RulesCheck. Strings stay UTF-8.
#pragma once

#include <string>
#include <vector>

namespace Rtt
{
	class JsonValue
	{
	public:
		enum class Kind { Null, Bool, Number, String, Array, Object };

		Kind kind = Kind::Null;
		bool boolean = false;
		double number = 0.0;
		std::string text;
		// arrays: the items; objects: the values, matching `keys`
		std::vector<JsonValue> items;
		std::vector<std::string> keys;

		bool IsNull() const { return kind == Kind::Null; }
		bool IsObject() const { return kind == Kind::Object; }
		bool IsArray() const { return kind == Kind::Array; }

		// Object member, or a shared null value when there is none.
		const JsonValue& operator[](const std::string& key) const;
		// Array item, or null past the end.
		const JsonValue& operator[](size_t index) const;
		bool Has(const std::string& key) const;
		size_t Size() const { return items.size(); }

		double Num(double fallback = 0.0) const;
		int Int(int fallback = 0) const;
		bool Bool(bool fallback = false) const;
		const std::string& Str() const;
		// Member shortcuts: a number / string / bool under `key`, or the fallback.
		double Num(const std::string& key, double fallback) const;
		int Int(const std::string& key, int fallback) const;
		std::string Str(const std::string& key, const std::string& fallback = "") const;
		bool Bool(const std::string& key, bool fallback) const;

		static const JsonValue& NullValue();
	};

	// Parses `text`; on a syntax error returns null and fills `error` (when given).
	JsonValue ParseJson(const std::string& text, std::string* error = nullptr);
	// Reads and parses a UTF-8 file.
	JsonValue LoadJsonFile(const std::string& path, std::string* error = nullptr);
}
