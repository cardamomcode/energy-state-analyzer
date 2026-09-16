#include <functional>
#include <string>
#include <vector>

int encode(int value) { return value; }

auto moduleBound = [](std::string value) { return value; };

std::vector<bool> outer(std::vector<bool> items) {
    auto localBound = [](bool value) { return value; };
    consume([](double item) { return item; });
    return items;
}

class Operations {
public:
    std::function<float(float)> classBound = [](float value) { return value; };

    long namedMethod(char value) { return value; }

    std::vector<double> outerMethod(std::vector<double> items) {
        auto localBound = [](double value) { return value; };
        consume([](short item) { return item; });
        return items;
    }
};
