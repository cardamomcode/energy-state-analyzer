#include <functional>
#include <string>

class StaticOperations {
public:
    inline static std::function<int(int)> parseNumber = [](int value) { return value; };
    inline static std::function<std::string(double)> formatNumber = [](double value) { return std::to_string(value); };
    inline static std::function<bool(char)> testCharacter = [](char value) { return value != 0; };
};
