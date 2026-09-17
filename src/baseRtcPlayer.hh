#pragma once

#include "basePlayer.hh"
#include "pipeline.hh"

namespace vptyp {

class BaseRTCPlayer : public BasePlayer {
 public:
  BaseRTCPlayer(GMainLoop& loop, const std::string& wsUri);
  ~BaseRTCPlayer() override = default;

  void create() override;
  void play() override;
  void stop() override;

 protected:
  std::string wsUri;
  GMainLoop& loop;
  Pipeline pipeline;
};

}  // namespace vptyp