#include <iostream>
#include <string>
#include <vector>

class DemoController {
public:
    DemoController() = default;

    void run() {
        std::vector<std::string> values = {"alpha", "beta", "gamma"};
        for (const auto& value : values) {
            std::cout << value << std::endl; // TODO: wire to editor demo output
        }
    }
};

int main() {
    DemoController controller;
    controller.run();
    return 0;
}
