#include <filesystem>

bool readConfig(std::string path) {
    return !path.empty();
}

bool writeConfig(std::string path, std::string data) {
    return !path.empty() && !data.empty();
}
