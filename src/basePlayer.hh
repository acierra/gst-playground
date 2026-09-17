#pragma once

#include "glib.h"
#include "pipeline.hh"
namespace vptyp {
class BasePlayer {
 public:
  BasePlayer() = default;
  virtual ~BasePlayer() = default;

  virtual void create() = 0;
  virtual void play() = 0;
  virtual void stop() = 0;
};

class VideoPlayback : public BasePlayer {
 public:
  VideoPlayback(GMainLoop& loop, std::string_view file);
  ~VideoPlayback() override = default;

  void create() override;
  void play() override;
  void stop() override;

 protected:
  std::string file;
  GMainLoop& loop;
  Pipeline pipeline;
};
}  // namespace vptyp