#include <vector>
#include <stdexcept>
#include <iostream>

struct Positive {
    int amount;
    explicit Positive(int value) : amount(value) {}
};

int flaggedPositive(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    return amount;
}

int flaggedUpperBound(int amount, int limit) {
    if (amount > 100) { throw std::invalid_argument("positive"); }
    return amount;
}

Positive cleanConstructed(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    return Positive(amount);
}

int cleanTransformed(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    return amount + 1;
}

int cleanIdentity(int amount, int limit) {
    return amount;
}

int cleanOtherInput(int amount, int limit) {
    if (limit <= 0) { throw std::invalid_argument("positive"); }
    return amount;
}

int cleanNestedThrow(int amount, int limit) {
    if (amount <= 0) { if (limit < 0) { throw std::invalid_argument("positive"); } }
    return amount;
}

int cleanInterveningWork(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    std::cout << amount;
    return amount;
}


std::vector<int> flaggedNonEmpty(std::vector<int> items) {
    if (items.empty()) { throw std::invalid_argument("positive"); }
    return items;
}

int* cleanNull(int* value) {
    if (value == nullptr) { throw std::invalid_argument("positive"); }
    return value;
}


std::string flaggedDispatch(std::string command) {
    if (command == "quit") { throw std::invalid_argument("bye"); }
    return command;
}


void flaggedCheckOnly(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
}

void flaggedExplicitEmpty(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
    return;
}

void flaggedBareReturn(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
    return;
}

void cleanGuardThenWork(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
    std::cout << amount;
}

void cleanNoCheck(int amount) {
    return;
}

void cleanConditionalThrow(int amount) {
    if (amount <= 0) { if (amount < -1) { throw std::invalid_argument("bad"); } }
}

void cleanUnconditionalThrow(int amount) {
    throw std::invalid_argument("bad");
}
