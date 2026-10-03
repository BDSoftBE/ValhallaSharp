#include <valhalla/tyr/actor.h>
#include <boost/property_tree/json_parser.hpp>
#include <cstdlib>
#include <cstring>
#include <exception>
#include <memory>
#include <sstream>
#include <stdexcept>

#ifdef _WIN32
#define VS_EXPORT extern "C" __declspec(dllexport)
#else
#define VS_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {
char* copy_string(const std::string& text) {
  auto* value = static_cast<char*>(std::malloc(text.size() + 1));
  if (!value) throw std::bad_alloc();
  std::memcpy(value, text.c_str(), text.size() + 1);
  return value;
}

template <typename F> int protect(char** error, F&& action) noexcept {
  if (!error) return 1;
  *error = nullptr;
  try { action(); return 0; }
  catch (const std::exception& e) {
    try { *error = copy_string(e.what()); } catch (...) {}
  }
  catch (...) {
    try { *error = copy_string("Unknown native Valhalla error."); } catch (...) {}
  }
  return 1;
}
}

VS_EXPORT int vs_actor_create(const char* config, void** actor, char** error) noexcept {
  if (actor) *actor = nullptr;
  return protect(error, [&] {
    if (!config || !actor) throw std::invalid_argument("Configuration and actor output are required.");
    boost::property_tree::ptree tree;
    std::istringstream stream(config);
    boost::property_tree::read_json(stream, tree);
    *actor = new valhalla::tyr::actor_t(tree, true);
  });
}

VS_EXPORT int vs_actor_request(void* actor, int action, const char* request,
                               char** result, char** error) noexcept {
  if (result) *result = nullptr;
  return protect(error, [&] {
    if (!actor || !request || !result) throw std::invalid_argument("Actor, request and result output are required.");
    auto& a = *static_cast<valhalla::tyr::actor_t*>(actor);
    std::string response;
    switch (action) {
      case 0: response = a.route(request); break;
      case 1: response = a.locate(request); break;
      case 2: response = a.matrix(request); break;
      case 3: response = a.optimized_route(request); break;
      case 4: response = a.isochrone(request); break;
      case 5: response = a.trace_route(request); break;
      case 6: response = a.trace_attributes(request); break;
      case 7: response = a.status(request); break;
      default: throw std::invalid_argument("Unsupported actor action.");
    }
    if (response.find('\0') != std::string::npos)
      throw std::invalid_argument("Binary responses are not supported by this JSON interface.");
    *result = copy_string(response);
  });
}

VS_EXPORT void vs_actor_destroy(void* actor) noexcept {
  try { delete static_cast<valhalla::tyr::actor_t*>(actor); } catch (...) {}
}

VS_EXPORT void vs_string_free(char* value) noexcept { std::free(value); }
