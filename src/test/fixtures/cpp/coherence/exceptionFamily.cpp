class ValidationError : public std::exception {
public:
    explicit ValidationError(std::string message) {}
};

class ParseError : public std::exception {
public:
    explicit ParseError(std::string message) {}
};

class TimeoutFailure : public std::exception {
public:
    explicit TimeoutFailure(std::string message) {}
};
