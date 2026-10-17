#include "Rules/RttJson.h"

#include <cstdio>
#include <cstdlib>

namespace Rtt
{
	namespace
	{
		struct Parser
		{
			const std::string& s;
			size_t i = 0;
			std::string error;

			explicit Parser(const std::string& text) : s(text) {}

			void SkipSpace()
			{
				while (i < s.size() && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
				{
					++i;
				}
			}

			bool Fail(const char* what)
			{
				if (error.empty())
				{
					error = std::string(what) + " at byte " + std::to_string(i);
				}
				return false;
			}

			bool Literal(const char* word)
			{
				size_t n = 0;
				while (word[n] != 0)
				{
					if (i + n >= s.size() || s[i + n] != word[n])
					{
						return Fail("bad literal");
					}
					++n;
				}
				i += n;
				return true;
			}

			static void PutUtf8(std::string& out, unsigned code)
			{
				if (code < 0x80)
				{
					out += static_cast<char>(code);
				}
				else if (code < 0x800)
				{
					out += static_cast<char>(0xC0 | (code >> 6));
					out += static_cast<char>(0x80 | (code & 0x3F));
				}
				else if (code < 0x10000)
				{
					out += static_cast<char>(0xE0 | (code >> 12));
					out += static_cast<char>(0x80 | ((code >> 6) & 0x3F));
					out += static_cast<char>(0x80 | (code & 0x3F));
				}
				else
				{
					out += static_cast<char>(0xF0 | (code >> 18));
					out += static_cast<char>(0x80 | ((code >> 12) & 0x3F));
					out += static_cast<char>(0x80 | ((code >> 6) & 0x3F));
					out += static_cast<char>(0x80 | (code & 0x3F));
				}
			}

			bool Hex4(unsigned& out)
			{
				if (i + 4 > s.size())
				{
					return Fail("short \\u escape");
				}
				out = 0;
				for (int k = 0; k < 4; ++k)
				{
					const char c = s[i++];
					out <<= 4;
					if (c >= '0' && c <= '9') out |= static_cast<unsigned>(c - '0');
					else if (c >= 'a' && c <= 'f') out |= static_cast<unsigned>(c - 'a' + 10);
					else if (c >= 'A' && c <= 'F') out |= static_cast<unsigned>(c - 'A' + 10);
					else return Fail("bad \\u escape");
				}
				return true;
			}

			bool String(std::string& out)
			{
				++i; // the opening quote
				while (i < s.size())
				{
					const char c = s[i++];
					if (c == '"')
					{
						return true;
					}
					if (c != '\\')
					{
						out += c;
						continue;
					}
					if (i >= s.size())
					{
						break;
					}
					const char e = s[i++];
					switch (e)
					{
					case '"': out += '"'; break;
					case '\\': out += '\\'; break;
					case '/': out += '/'; break;
					case 'b': out += '\b'; break;
					case 'f': out += '\f'; break;
					case 'n': out += '\n'; break;
					case 'r': out += '\r'; break;
					case 't': out += '\t'; break;
					case 'u':
					{
						unsigned code = 0;
						if (!Hex4(code))
						{
							return false;
						}
						// a surrogate pair makes one code point
						if (code >= 0xD800 && code <= 0xDBFF && i + 1 < s.size() && s[i] == '\\' && s[i + 1] == 'u')
						{
							i += 2;
							unsigned low = 0;
							if (!Hex4(low))
							{
								return false;
							}
							code = 0x10000 + ((code - 0xD800) << 10) + (low - 0xDC00);
						}
						PutUtf8(out, code);
						break;
					}
					default:
						return Fail("bad escape");
					}
				}
				return Fail("unterminated string");
			}

			bool Value(JsonValue& out)
			{
				SkipSpace();
				if (i >= s.size())
				{
					return Fail("unexpected end");
				}
				const char c = s[i];
				if (c == '{')
				{
					out.kind = JsonValue::Kind::Object;
					++i;
					SkipSpace();
					if (i < s.size() && s[i] == '}')
					{
						++i;
						return true;
					}
					while (true)
					{
						SkipSpace();
						if (i >= s.size() || s[i] != '"')
						{
							return Fail("expected a key");
						}
						std::string key;
						if (!String(key))
						{
							return false;
						}
						SkipSpace();
						if (i >= s.size() || s[i] != ':')
						{
							return Fail("expected ':'");
						}
						++i;
						JsonValue v;
						if (!Value(v))
						{
							return false;
						}
						out.keys.push_back(key);
						out.items.push_back(std::move(v));
						SkipSpace();
						if (i < s.size() && s[i] == ',')
						{
							++i;
							continue;
						}
						if (i < s.size() && s[i] == '}')
						{
							++i;
							return true;
						}
						return Fail("expected ',' or '}'");
					}
				}
				if (c == '[')
				{
					out.kind = JsonValue::Kind::Array;
					++i;
					SkipSpace();
					if (i < s.size() && s[i] == ']')
					{
						++i;
						return true;
					}
					while (true)
					{
						JsonValue v;
						if (!Value(v))
						{
							return false;
						}
						out.items.push_back(std::move(v));
						SkipSpace();
						if (i < s.size() && s[i] == ',')
						{
							++i;
							continue;
						}
						if (i < s.size() && s[i] == ']')
						{
							++i;
							return true;
						}
						return Fail("expected ',' or ']'");
					}
				}
				if (c == '"')
				{
					out.kind = JsonValue::Kind::String;
					return String(out.text);
				}
				if (c == 't')
				{
					out.kind = JsonValue::Kind::Bool;
					out.boolean = true;
					return Literal("true");
				}
				if (c == 'f')
				{
					out.kind = JsonValue::Kind::Bool;
					out.boolean = false;
					return Literal("false");
				}
				if (c == 'n')
				{
					out.kind = JsonValue::Kind::Null;
					return Literal("null");
				}
				// a number
				const char* start = s.c_str() + i;
				char* end = nullptr;
				const double v = std::strtod(start, &end);
				if (end == start)
				{
					return Fail("bad value");
				}
				out.kind = JsonValue::Kind::Number;
				out.number = v;
				i += static_cast<size_t>(end - start);
				return true;
			}
		};
	}

	const JsonValue& JsonValue::NullValue()
	{
		static const JsonValue Null;
		return Null;
	}

	const JsonValue& JsonValue::operator[](const std::string& key) const
	{
		if (kind == Kind::Object)
		{
			for (size_t k = 0; k < keys.size(); ++k)
			{
				if (keys[k] == key)
				{
					return items[k];
				}
			}
		}
		return NullValue();
	}

	const JsonValue& JsonValue::operator[](size_t index) const
	{
		return index < items.size() ? items[index] : NullValue();
	}

	bool JsonValue::Has(const std::string& key) const
	{
		if (kind != Kind::Object)
		{
			return false;
		}
		for (const std::string& k : keys)
		{
			if (k == key)
			{
				return true;
			}
		}
		return false;
	}

	double JsonValue::Num(double fallback) const
	{
		if (kind == Kind::Number) return number;
		if (kind == Kind::Bool) return boolean ? 1.0 : 0.0;
		return fallback;
	}

	int JsonValue::Int(int fallback) const
	{
		return kind == Kind::Number || kind == Kind::Bool ? static_cast<int>(Num() + (Num() < 0.0 ? -0.5 : 0.5)) : fallback;
	}

	bool JsonValue::Bool(bool fallback) const
	{
		if (kind == Kind::Bool) return boolean;
		if (kind == Kind::Number) return number != 0.0;
		return fallback;
	}

	const std::string& JsonValue::Str() const
	{
		static const std::string Empty;
		return kind == Kind::String ? text : Empty;
	}

	double JsonValue::Num(const std::string& key, double fallback) const { return (*this)[key].Num(fallback); }
	int JsonValue::Int(const std::string& key, int fallback) const { return (*this)[key].Int(fallback); }
	bool JsonValue::Bool(const std::string& key, bool fallback) const { return (*this)[key].Bool(fallback); }

	std::string JsonValue::Str(const std::string& key, const std::string& fallback) const
	{
		const JsonValue& v = (*this)[key];
		return v.kind == Kind::String ? v.text : fallback;
	}

	JsonValue ParseJson(const std::string& text, std::string* error)
	{
		Parser p(text);
		JsonValue out;
		// a UTF-8 byte order mark is allowed
		if (text.size() >= 3 && static_cast<unsigned char>(text[0]) == 0xEF && static_cast<unsigned char>(text[1]) == 0xBB
			&& static_cast<unsigned char>(text[2]) == 0xBF)
		{
			p.i = 3;
		}
		if (!p.Value(out))
		{
			if (error)
			{
				*error = p.error;
			}
			return JsonValue();
		}
		return out;
	}

	JsonValue LoadJsonFile(const std::string& path, std::string* error)
	{
		FILE* f = nullptr;
#if defined(_MSC_VER)
		if (fopen_s(&f, path.c_str(), "rb") != 0)
		{
			f = nullptr;
		}
#else
		f = std::fopen(path.c_str(), "rb");
#endif
		if (!f)
		{
			if (error)
			{
				*error = "cannot open " + path;
			}
			return JsonValue();
		}
		std::string text;
		char buf[65536];
		size_t n = 0;
		while ((n = std::fread(buf, 1, sizeof(buf), f)) > 0)
		{
			text.append(buf, n);
		}
		std::fclose(f);
		return ParseJson(text, error);
	}
}
