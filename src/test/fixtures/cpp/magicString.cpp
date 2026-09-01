std::string cleanValues(std::string name, Config config) {
    std::string message = "user " + name + " not found";
    log("something went wrong");
    return message + config["timeout"];
}

int flaggedMagicString(std::string status) {
    if (status == "pending") {
        return 1;
    }
    if (status == "pending") {
        return 2;
    }
    return 0;
}

int flaggedDictKey(Config config, Config other) {
    return config["retries"] + other["retries"];
}
