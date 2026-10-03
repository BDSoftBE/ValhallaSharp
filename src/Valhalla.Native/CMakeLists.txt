cmake_minimum_required(VERSION 3.24)
project(ValhallaSharpNative LANGUAGES C CXX)
include(FetchContent)
find_package(Boost 1.71 REQUIRED)

set(VALHALLA_SOURCE_DIR_OVERRIDE "" CACHE PATH "Existing recursive Valhalla checkout (optional)")
set(VALHALLASHARP_OUTPUT_DIR "${CMAKE_BINARY_DIR}/out" CACHE PATH "Native runtime output")
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
foreach(option ENABLE_TOOLS ENABLE_DATA_TOOLS ENABLE_SERVICES ENABLE_HTTP ENABLE_PYTHON_BINDINGS ENABLE_NODE_BINDINGS ENABLE_TESTS ENABLE_GEOTIFF ENABLE_LZ4 ENABLE_CCACHE)
  set(${option} OFF CACHE BOOL "" FORCE)
endforeach()
if(VALHALLA_SOURCE_DIR_OVERRIDE)
  add_subdirectory("${VALHALLA_SOURCE_DIR_OVERRIDE}" valhalla)
else()
  FetchContent_Declare(valhalla
    GIT_REPOSITORY https://github.com/valhalla/valhalla.git
    GIT_TAG 53e00619f642c32896dd77afa3ee2f792c6f75d3
    GIT_SUBMODULES_RECURSE TRUE)
  FetchContent_MakeAvailable(valhalla)
endif()

add_library(valhalla_sharp SHARED bridge.cpp)
target_compile_features(valhalla_sharp PRIVATE cxx_std_20)
target_link_libraries(valhalla_sharp PRIVATE valhalla Boost::boost)
set_target_properties(valhalla_sharp PROPERTIES
  RUNTIME_OUTPUT_DIRECTORY "${VALHALLASHARP_OUTPUT_DIR}"
  LIBRARY_OUTPUT_DIRECTORY "${VALHALLASHARP_OUTPUT_DIR}")
foreach(config Debug Release RelWithDebInfo MinSizeRel)
  string(TOUPPER "${config}" upper)
  set_target_properties(valhalla_sharp PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY_${upper} "${VALHALLASHARP_OUTPUT_DIR}")
endforeach()
# Use static dependencies so deployment only needs this DLL and the MSVC runtime.
if(DEFINED VCPKG_TARGET_TRIPLET AND NOT VCPKG_TARGET_TRIPLET STREQUAL "x64-windows-static-md")
  message(FATAL_ERROR "Use VCPKG_TARGET_TRIPLET=x64-windows-static-md to avoid missing dependency DLLs")
endif()
