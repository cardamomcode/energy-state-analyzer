fn matchArmNested(value: i32, enabled: bool) -> i32 {
    match value {
        0 => {
            if enabled { check(); }
            2
        },
        _ => 1,
    }
}

fn matchArmPlain(value: i32) -> i32 {
    match value {
        0 => 2,
        _ => 1,
    }
}
