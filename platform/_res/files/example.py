import sys


def hello(name: str) -> str:
    return f"Hello, {name}"


if __name__ == "__main__":
    print(hello("Pydroid"))
    print("Python version:", sys.version)
