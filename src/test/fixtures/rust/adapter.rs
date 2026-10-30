use std::collections::{HashMap, HashSet};
use crate::values as local_values;

fn clean_values(left: i32, right: i32) -> i32 {
    left + right
}

fn flagged_parameters(a: i32, b: i32, c: i32, d: i32, e: i32, f: i32) -> i32 {
    a + b + c + d + e + f
}

fn flagged_boolean(left: bool, right: bool) -> bool {
    left && right
}

fn flagged_magic(value: i32) -> bool {
    value == 37
}

fn flagged_string(value: &str) -> bool {
    value == "active" || value == "active"
}

fn clean_string(value: &str) -> bool {
    value == "active"
}

fn flagged_opaque() {
    record(true);
}

fn clean_named_boolean() {
    let enabled = true;
    record(enabled);
}

fn branching(value: i32, enabled: bool) -> i32 {
    if enabled && value > 0 {
        for item in 0..value {
            if item > 2 {
                return item;
            }
        }
    }

    match value {
        0 => 0,
        1 => 1,
        _ => 2,
    }
}

fn flagged_complexity(value: i32) -> i32 {
    let mut result = value;
    if value > 1 { result += 1; }
    if value > 2 { result += 1; }
    if value > 3 { result += 1; }
    if value > 4 { result += 1; }
    if value > 5 { result += 1; }
    if value > 6 { result += 1; }
    if value > 7 { result += 1; }
    if value > 8 { result += 1; }
    if value > 9 { result += 1; }
    if value > 10 { result += 1; }
    result
}

fn flagged_deep(a: bool, b: bool, c: bool, d: bool, e: bool, f: bool) {
    if a {
        if b {
            if c {
                if d {
                    if e {
                        if f {
                            check();
                        }
                    }
                }
            }
        }
    }
}

fn flagged_match_opportunity(value: i32) -> i32 {
    if value == 2 { 2 }
    else if value == 3 { 3 }
    else if value == 4 { 4 }
    else { 0 }
}

fn clean_match(value: i32) -> i32 {
    match value {
        2 => 2,
        3 => 3,
        4 => 4,
        _ => 0,
    }
}

fn clean_validator(value: i32) -> i32 {
    if value < 0 { panic!("invalid"); }
    value
}

fn flagged_logical(enabled: bool) {
    enabled && check();
}

fn closure_owner() {
    let handler = |a: i32, b: i32, c: i32, d: i32, e: i32, f: i32| {
        if a > b && c > d { e } else { f }
    };
    handler(1, 2, 3, 4, 5, 6);
}

fn untyped_closure_owner() {
    let handler = |a, b, c, d, e, f| a + b + c + d + e + f;
    handler(1, 2, 3, 4, 5, 6);
}

struct Engine {
    value: i32,
}

impl Engine {
    fn method(&self, input: i32) -> i32 {
        self.value + input
    }
}
