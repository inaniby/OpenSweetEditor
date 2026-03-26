cmake_minimum_required(VERSION 3.22.1)
project(pybridge)

add_library(pybridge SHARED python_bridge.c)

find_library(log-lib log)

target_link_libraries(pybridge ${log-lib} dl)
