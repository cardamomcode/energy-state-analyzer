#include <string>
#include <vector>
#include <stdexcept>
#include <iostream>

struct Positive {
    int amount;
    explicit Positive(int value) : amount(value) {}
};

// flagged — parse, don't validate
int flaggedPositive(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    return amount;
}

// flagged — parse, don't validate
int flaggedUpperBound(int amount, int limit) {
    if (amount > 100) { throw std::invalid_argument("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
Positive cleanConstructed(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    return Positive(amount);
}

// clean — not flagged by parse, don't validate
int cleanTransformed(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    return amount + 1;
}

// clean — not flagged by parse, don't validate
int cleanIdentity(int amount, int limit) {
    return amount;
}

// clean — not flagged by parse, don't validate
int cleanOtherInput(int amount, int limit) {
    if (limit <= 0) { throw std::invalid_argument("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
int cleanNestedThrow(int amount, int limit) {
    if (amount <= 0) { if (limit < 0) { throw std::invalid_argument("positive"); } }
    return amount;
}

// clean — not flagged by parse, don't validate
int cleanInterveningWork(int amount, int limit) {
    if (amount <= 0) { throw std::invalid_argument("positive"); }
    std::cout << amount;
    return amount;
}


// flagged — parse, don't validate
std::vector<int> flaggedNonEmpty(std::vector<int> items) {
    if (items.empty()) { throw std::invalid_argument("positive"); }
    return items;
}

// clean — not flagged by parse, don't validate
int* cleanNull(int* value) {
    if (value == nullptr) { throw std::invalid_argument("positive"); }
    return value;
}


// flagged — parse, don't validate
std::string flaggedDispatch(std::string command) {
    if (command == "quit") { throw std::invalid_argument("bye"); }
    return command;
}


// flagged — parse, don't validate
void flaggedCheckOnly(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
}

// flagged — parse, don't validate
void flaggedExplicitEmpty(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
    return;
}

// flagged — parse, don't validate
void flaggedBareReturn(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
    return;
}

// clean — not flagged by parse, don't validate
void cleanGuardThenWork(int amount) {
    if (amount <= 0) { throw std::invalid_argument("bad"); }
    std::cout << amount;
}

// clean — not flagged by parse, don't validate
void cleanNoCheck(int amount) {
    return;
}

// clean — not flagged by parse, don't validate
void cleanConditionalThrow(int amount) {
    if (amount <= 0) { if (amount < -1) { throw std::invalid_argument("bad"); } }
}

// clean — not flagged by parse, don't validate
void cleanUnconditionalThrow(int amount) {
    throw std::invalid_argument("bad");
}


// flagged — parse, don't validate
bool flaggedBooleanValidator(const char* pw) {
    if (pw[0] == '\0') { return false; }
    return true;
}

// flagged — parse, don't validate
bool flaggedThrowingBoolean(const char* pw) {
    if (pw[0] == '\0') { throw std::invalid_argument("empty"); }
    return true;
}

// flagged — parse, don't validate
bool flaggedNullBooleanValidator(const char* value) {
    if (value == nullptr) { return false; }
    return true;
}

// clean — not flagged by parse, don't validate
bool cleanBooleanQuery(const std::string& user) {
    if (user.empty()) { return false; }
    return user == "admin";
}

// flagged — C++ has no standard narrowing annotation a signature can carry, so this
// null-checking boolean validator still discards the checked value (limitation case for
// the shared explicit-narrowing row).
bool cleanExplicitNarrowingValidator(const char* value) {
    if (value == nullptr) { return false; }
    return true;
}
