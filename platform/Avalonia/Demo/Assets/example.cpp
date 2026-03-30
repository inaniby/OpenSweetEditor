// SweetEditor Demo
// Snapshot-aligned C++ sample for rendering, folding and decorations.

#include <iostream>
#include <string>
#include <vector>

//===============================================================
namespace editor {

class Logger {
public:
    enum Level { DEBUG, INFO, WARN, ERROR };

    void log(Level level, const std::string& msg) {
        const char* tags[] = {"D", "I", "W", "E"};
        const unsigned int colors[] = {0XFF4CAF50, 0XFF2196F3, 0XFFFF9800, 0XFFF44336};
        std::cout << "[" << tags[level] << "] " << msg << std::endl;
    }

    void warn(const std::string& m) { log(WARN, m); }
};  // end class Logger

//---------------------------------------------------------------
struct Token { int kind; int start; int length; };

std::vector<Token> tokenize(const std::string& line) {
    std::vector<Token> result;
    for (size_t i = 0; i < line.size(); ++i) {
        switch (line[i]) {
        case '#':
            result.push_back({1, static_cast<int>(i), 1});
            break;
        case '"':
            result.push_back({2, static_cast<int>(i), 1});
            break;
        case '/':
            result.push_back({3, static_cast<int>(i), 1});
            break;
        default:
            result.push_back({0, static_cast<int>(i), 1});
            break;
        }
    }
    return result;
}

}  // namespace editor

//===============================================================
int main() {
    editor::Logger logger;
    auto tokens = editor::tokenize("int value = 42;");

    for (const auto& token : tokens) {
        logger.log(editor::Logger::INFO, "token kind=" + std::to_string(token.kind));
    }

    return 0;
}
