class CancellationToken;

class CancellationTokenSource {
public:
    CancellationToken token();
    void cancel() {}
};

class CancellationToken {
public:
    explicit CancellationToken(CancellationTokenSource source) {}
    bool isCancellationRequested() { return false; }
};

CancellationToken CancellationTokenSource::token() {
    return CancellationToken(*this);
}
