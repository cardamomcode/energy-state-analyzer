#include <functional>

int named(int value) {
    return value;
}

auto moduleBound = [offset](int left, int right) {
    return left + right;
};

class Operations {
public:
    std::function<int(int)> classBound = [](int value) {
        return value + 1;
    };

    int namedMethod(int value) {
        return value;
    }
};

int outer() {
    auto localBound = [](int value) {
        return value * 2;
    };
    consume([](int item) { return item + 1; });
    return 0;
}
