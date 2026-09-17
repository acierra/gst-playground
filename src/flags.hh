#pragma once
#include <string>

namespace vptyp {
struct Flags {
  std::string url{};
  std::string filename{};
  std::string output{};
  std::string wsUri{};
};

void init_flags(const Flags&);

const Flags& get_flags();

}  // namespace vptyp