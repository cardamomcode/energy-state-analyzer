class Logger {
public:
    explicit Logger(std::string prefix) {}
    void log(std::string message) {}
};

class HttpClient {
public:
    explicit HttpClient(std::string baseUrl) {}
    std::string get(std::string path) { return path; }
};
