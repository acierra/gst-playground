#include "flags.hh"

namespace vptyp {

static Flags flags{};

void init_flags(const Flags& f) { flags = f; }

const Flags& get_flags() { return flags; }

}  // namespace vptyp