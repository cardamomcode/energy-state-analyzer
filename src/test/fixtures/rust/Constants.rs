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
