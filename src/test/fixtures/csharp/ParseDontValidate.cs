using System;
using System.Collections.Generic;

record Positive(int Amount);

class Validators {
int FlaggedPositive(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    return amount;
}

int FlaggedUpperBound(int amount, int limit) {
    if (amount > 100) { throw new Exception("positive"); }
    return amount;
}

Positive CleanConstructed(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    return new Positive(amount);
}

int CleanTransformed(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    return amount + 1;
}

int CleanIdentity(int amount, int limit) {
    return amount;
}

int CleanOtherInput(int amount, int limit) {
    if (limit <= 0) { throw new Exception("positive"); }
    return amount;
}

int CleanNestedThrow(int amount, int limit) {
    if (amount <= 0) { if (limit < 0) { throw new Exception("positive"); } }
    return amount;
}

int CleanInterveningWork(int amount, int limit) {
    if (amount <= 0) { throw new Exception("positive"); }
    Console.WriteLine(amount);
    return amount;
}

List<int> FlaggedNonEmpty(List<int> items) {
    if (items.Count == 0) { throw new Exception("empty"); }
    return items;
}

string CleanNull(string? value) {
    if (value == null) { throw new Exception("missing"); }
    return value;
}

string? CleanNullCheck(string? value) {
    if (string.IsNullOrEmpty(value)) { throw new Exception("missing"); }
    return value;
}


void FlaggedCheckOnly(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
}

void FlaggedExplicitEmpty(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
    return;
}

void FlaggedBareReturn(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
    return;
}

void CleanGuardThenWork(int amount) {
    if (amount <= 0) { throw new Exception("bad"); }
    Console.WriteLine(amount);
}

void CleanNoCheck(int amount) {
    return;
}

void CleanConditionalThrow(int amount) {
    if (amount <= 0) { if (amount < -1) { throw new Exception("bad"); } }
}

void CleanUnconditionalThrow(int amount) {
    throw new Exception("bad");
}
}

class CheckedAmount {
public CheckedAmount(int amount) {
    if (amount <= 0) { throw new Exception("positive"); }
}
}
