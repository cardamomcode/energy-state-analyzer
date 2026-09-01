#include <vector>
#include "thing.hpp"

constexpr int GlobalLimit = 42;

enum class Mode {
    Fast = 5,
    Slow
};

struct Derived final : public ns::Base, virtual Interface<int> {
    std::string method(const std::string& name, int count = 7, int* pointer = nullptr) const {
        for (int i = 0; i < count; ++i) {
            if (name == "x" and pointer[i]) {
                return name;
            }
        }
        return "none";
    }
};

auto trailing(std::vector<int> values) -> std::string {
    for (int value : values) {
        consume(value);
    }
    while (false) {}
    do {} while (false);
    return "done";
}
