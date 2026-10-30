fn production_number(value: i32) -> bool {
    value == 37
}

fn production_string(value: &str) -> bool {
    value == "ready" || value == "ready"
}

fn production_single(value: &str) -> bool {
    value == "shared"
}

fn production_boolean() {
    record(true);
}

#[cfg(test)]
mod tests {
    fn named_number(value: i32) -> bool {
        value == 42
    }

    fn named_string(value: &str) -> bool {
        value == "pending" || value == "pending" || value == "shared"
    }

    fn named_boolean() {
        record(true);
    }
}

#[allow(dead_code)]
#[cfg(test)]
mod checks {
    fn attributed_number(value: i32) -> bool {
        value == 43
    }
}

mod smoke_tests {
    fn plural_number(value: i32) -> bool {
        value == 44
    }
}

#[cfg(feature = "test")]
mod feature_code {
    fn feature_number(value: i32) -> bool {
        value == 45
    }
}

#[cfg(not(test))]
mod release_code {
    fn release_number(value: i32) -> bool {
        value == 46
    }
}

fn later_production_number(value: i32) -> bool {
    value == 47
}
