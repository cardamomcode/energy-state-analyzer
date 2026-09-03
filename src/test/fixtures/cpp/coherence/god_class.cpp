// decision: a class whose methods each solve a different problem (DB, email, PDF, imaging, auth,
// queueing...). Over the method-count bar AND spanning many unrelated domain types, so it is
// flagged as a god class. The marker types below exist only to be "unrelated" to each other.
#include <string>
#include <vector>

struct Connection {}; // fixture-only marker types
struct Row {};
struct Image {};
struct Report {};
struct Token {};

class GodService {
public:
    std::vector<object> state;

    Row fetchRows(Connection conn) {
        return Row{};
    }

    bool sendEmail(std::string to, std::string body) {
        return true;
    }

    std::bytes renderPdf(std::map<std::string, int> data) {
        return std::bytes{};
    }

    std::string compress(const std::string& path) {
        return path;
    }

    bool validateToken(Token token) {
        return true;
    }

    void notify(const std::string& message) {
        // noop
    }

    std::string exportCsv(std::vector<int> rows) {
        return "";
    }

    Image resize(Image image) {
        return image;
    }

    std::map<std::string, int> parseYaml(const std::string& text) {
        return {};
    }

    std::string hashPassword(const std::string& password) {
        return "";
    }

    bool sendSms(const std::string& number, const std::string& message) {
        return true;
    }

    Report buildReport(std::map<std::string, int> data) {
        return Report{};
    }

    std::bytes encrypt(std::bytes raw) {
        return raw;
    }

    std::string schedule(object job) {
        return "";
    }

    object cacheGet(const std::string& key) {
        return new object();
    }

    void logEvent(const std::string& event) {
        // noop
    }
};
