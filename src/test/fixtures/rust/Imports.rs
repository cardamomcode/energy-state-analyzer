fn clean_import() {
    use std::collections::HashMap;
    let _value: HashMap<i32, i32> = HashMap::new();
}

fn flagged_wildcard() {
    use std::io::*;
    let _stdin = stdin();
}
