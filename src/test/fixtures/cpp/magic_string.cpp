// clean — not flagged by magic string
std::string cleanValues(std::string name, Config config) {
    std::string message = "user " + name + " not found";
    log("something went wrong");
    return message + config["timeout"];
}

// flagged — magic string
int flaggedMagicString(std::string status) {
    if (status == "pending") {
        return 1;
    }
    if (status == "pending") {
        return 2;
    }
    return 0;
}

// flagged — magic string
int flaggedDictKey(Config config, Config other) {
    return config["retries"] + other["retries"];
}

// clean — not flagged by magic string; C++ switch labels cannot be strings, so only results appear here
std::string cleanCaseResult(int code) {
    switch (code) {
        case 1:
            return "result";
        default:
            return "result";
    }
}

// flagged — magic string
bool flaggedInequality(std::string status, std::string other) {
    return status != "archived" && other != "archived";
}
