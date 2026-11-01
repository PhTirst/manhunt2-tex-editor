cmake_minimum_required(VERSION 3.24)
project(Manhunt2TexEditor VERSION 0.1.0 LANGUAGES C CXX)
if(NOT WIN32 OR NOT MSVC OR NOT CMAKE_SIZEOF_VOID_P EQUAL 8)
    message(FATAL_ERROR "Windows x64 with clang-cl or MSVC is required.")
endif()
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
include(cmake/Dependencies.cmake)
add_library(mh2core SHARED
    src/native/mh2_core_c_api.cpp
    src/native/mh2_tex_document.cpp
    src/native/mh2_dds.cpp
    src/native/mh2_tex.cpp)
target_compile_features(mh2core PRIVATE cxx_std_17)
target_compile_definitions(mh2core PRIVATE MH2CORE_EXPORTS WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_options(mh2core PRIVATE /W4 /permissive- /utf-8)
target_link_libraries(mh2core PRIVATE zlibstatic DirectXTex ole32 windowscodecs)
