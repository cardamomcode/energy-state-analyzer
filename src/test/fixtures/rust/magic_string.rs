// Magic-string decision points that Rust spells as patterns or wrapped constructor operands.

fn flagged_case_pattern(kind: Option<&str>) -> u8 {
    match kind {
        Some("started") => 1,
        Some("stopped" | "started") => 2,
        _ => 0,
    }
}

fn clean_case_result(kind: &str) -> &str {
    match kind {
        "opened" => "result",
        _ => "result",
    }
}

fn clean_guard_body(kind: &str, label: &str) -> &'static str {
    match kind {
        "single" if label.is_empty() => "outcome",
        _ => "outcome",
    }
}

fn flagged_if_let(kind: Option<&str>, other: Option<&str>) -> bool {
    if let Some("paused") = kind {
        return true;
    }
    if let Some("paused") = other {
        return true;
    }
    false
}

fn flagged_wrapped_equality(kind: Option<&str>, other: Option<&str>) -> bool {
    kind == Some("analysis") || other == Some("analysis")
}

fn clean_call_argument(kind: &str, other: &str) -> bool {
    kind == label("closing") || other == label("closing")
}

fn clean_macro_pattern(kind: Option<&str>, other: Option<&str>) -> bool {
    matches!(kind, Some("draining")) || matches!(other, Some("draining"))
}

fn label(text: &str) -> &str {
    text
}
