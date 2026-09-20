using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

record Positive(int Amount);

class Validators {
// flagged — parse, don't validate
int FlaggedPositive(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    return amount;
}

// flagged — parse, don't validate
int FlaggedUpperBound(int amount, int limit) {
    if (amount > 100) { throw new Exception("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
Positive CleanConstructed(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    return new Positive(amount);
}

// clean — not flagged by parse, don't validate
int CleanTransformed(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    return amount + 1;
}

// clean — not flagged by parse, don't validate
int CleanIdentity(int amount, int limit) {
    return amount;
}

// clean — not flagged by parse, don't validate
int CleanOtherInput(int amount, int limit) {
    if (limit <= 0) { throw new Exception("positive"); }
    return amount;
}

// clean — not flagged by parse, don't validate
int CleanNestedThrow(int amount, int limit) {
    if (amount <= 0) { if (limit < 0) { throw new Exception("positive"); } }
    return amount;
}

// clean — not flagged by parse, don't validate
int CleanInterveningWork(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    Console.WriteLine(amount);
    return amount;
}

// flagged — parse, don't validate
List<int> FlaggedNonEmpty(List<int> items) {
    if (items.Count == 0) { throw new Exception("empty"); }
    return items;
}

// clean — not flagged by parse, don't validate
string CleanNull(string? value) {
    if (value == null) { throw new Exception("missing"); }
    return value;
}

// flagged — parse, don't validate (low)
string? CleanNullCheck(string? value) {
    if (string.IsNullOrEmpty(value)) { throw new Exception("missing"); }
    return value;
}

// flagged — parse, don't validate
string FlaggedDispatch(string command) {
    if (command == "quit") { throw new Exception("bye"); }
    return command;
}


// flagged — parse, don't validate
void FlaggedCheckOnly(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
}

// flagged — parse, don't validate
void FlaggedExplicitEmpty(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
    return;
}

// flagged — parse, don't validate
void FlaggedBareReturn(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
    return;
}

// clean — not flagged by parse, don't validate
void CleanGuardThenWork(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
    Console.WriteLine(amount);
}

// clean — not flagged by parse, don't validate
void CleanNoCheck(int amount) {
    return;
}

// clean — not flagged by parse, don't validate
void CleanConditionalThrow(int amount) {
    if (amount <= 0) { if (amount < -1) { throw new Exception("bad"); } }
}

// clean — not flagged by parse, don't validate
void CleanUnconditionalThrow(int amount) {
    throw new Exception("bad");
}

// flagged — parse, don't validate
bool FlaggedBooleanValidator(string pw) {
    if (pw.IndexOf('@') < 0) { return false; }
    return true;
}

// flagged — parse, don't validate
bool FlaggedThrowingBoolean(string pw) {
    if (pw.Length == 0) { throw new Exception("empty"); }
    return true;
}

// flagged — parse, don't validate
bool FlaggedNullBooleanValidator(string? value) {
    if (value == null) { return false; }
    return true;
}

// clean — not flagged by parse, don't validate
bool CleanBooleanQuery(string user) {
    if (user.Length == 0) { return false; }
    return user == "admin";
}

// clean — not flagged by parse, don't validate
bool CleanExplicitNarrowingValidator([NotNullWhen(true)] string? value) {
    if (value == null) { return false; }
    return true;
}

// flagged — an annotation on a different parameter does not carry this checked property
bool FlaggedUnrelatedNarrowingValidator([NotNullWhen(true)] string narrowed, string value) {
    if (value.Length == 0) { return false; }
    return true;
}

// clean — not flagged by parse, don't validate (a truthy literal from the guard branch is not a rejection)
bool CleanFlipped(string? value) {
    if (value != null) { return true; }
    return false;
}
}

class CheckedAmount {
// clean — not flagged by parse, don't validate
public CheckedAmount(int amount) {
    if (amount <= 0) { throw new Exception("positive"); }
}
}
