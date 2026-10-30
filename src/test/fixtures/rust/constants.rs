const BUFFER_SIZE: usize = 32;
static RETRIES: i32 = 42;

enum Level {
    Low = 3,
    High = 4,
}

type Buffer = [u8; 16];

fn flagged_runtime(value: i32) -> bool {
    value == 37
}

fn flagged_u32(value: u32) -> bool {
    value == 37u32
}

fn flagged_i32(value: i32) -> bool {
    value == 37_i32
}

fn flagged_f64(value: f64) -> bool {
    value == 3.14f64
}

fn clean_allowlisted_suffix(value: u32) -> bool {
    value == 1u32
}
