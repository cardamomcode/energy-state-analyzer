struct Positive(i32);
struct NonEmpty(Vec<i32>);

fn flagged_result_guard(value: i32) -> Result<i32, &'static str> {
    if value <= 0 {
        return Err("must be positive");
    }
    Ok(value)
}

fn flagged_option_guard(items: Vec<i32>) -> Option<Vec<i32>> {
    if items.is_empty() {
        return None;
    }
    Some(items)
}

fn flagged_explicit_return(value: i32) -> Result<i32, &'static str> {
    if value <= 0 {
        return Err("must be positive");
    }
    return Ok(value);
}

fn flagged_result_match(value: i32) -> Result<i32, &'static str> {
    match value {
        0 => Err("zero"),
        _ => Ok(value),
    }
}

fn flagged_option_match(items: Vec<i32>) -> Option<Vec<i32>> {
    match items.len() {
        0 => None,
        _ => Some(items),
    }
}

fn clean_constructed(value: i32) -> Result<Positive, &'static str> {
    if value <= 0 {
        return Err("must be positive");
    }
    Ok(Positive(value))
}

fn clean_option_constructed(items: Vec<i32>) -> Option<NonEmpty> {
    if items.is_empty() {
        return None;
    }
    Some(NonEmpty(items))
}

fn clean_transformed(value: i32) -> Result<i32, &'static str> {
    if value <= 0 {
        return Err("must be positive");
    }
    Ok(value + 1)
}

fn clean_other_input(value: i32, fallback: i32) -> Result<i32, &'static str> {
    if value <= 0 {
        return Err("must be positive");
    }
    Ok(fallback)
}

fn clean_unrelated_result(value: i32) -> Result<i32, &'static str> {
    Ok(value)
}

fn clean_unrelated_option(items: Vec<i32>) -> Option<Vec<i32>> {
    Some(items)
}

fn clean_intervening_work(value: i32) -> Result<i32, &'static str> {
    if value <= 0 {
        return Err("must be positive");
    }
    log(value);
    Ok(value)
}

fn clean_nested_rejection(value: i32, enabled: bool) -> Result<i32, &'static str> {
    if value <= 0 {
        if enabled {
            return Err("must be positive");
        }
    }
    Ok(value)
}

fn clean_panic(value: i32) -> Result<i32, &'static str> {
    if value <= 0 {
        panic!("must be positive");
    }
    Ok(value)
}

fn clean_guarded_match(value: i32) -> Result<i32, &'static str> {
    match value {
        n if n <= 0 => Err("must be positive"),
        _ => Ok(value),
    }
}

fn clean_guarded_literal_match(value: i32) -> Result<i32, &'static str> {
    match value {
        0 if condition() => Err("zero"),
        _ => Ok(value),
    }
}
