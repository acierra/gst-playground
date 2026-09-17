#pragma once
#include "basePlayer.hh"
#include "flags.hh"
namespace vptyp {

class PlayerFactory {
 public:
  std::unique_ptr<BasePlayer> create(const Flags& flags, GMainLoop& loop);
};

}  // namespace vptyp