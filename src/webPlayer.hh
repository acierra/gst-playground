#pragma once

#include "basePlayer.hh"
#include "pipeline.hh"

namespace vptyp {
class WebToFilePlayer : public BasePlayer {
 public:
  WebToFilePlayer(GMainLoop& loop, std::string_view url,
                  std::string_view output_file);
  ~WebToFilePlayer() override = default;

  void create() override;
  void play() override;
  void stop() override;

 protected:
  std::string url;
  std::string output_file;
  GMainLoop& loop;
  Pipeline pipeline;
};
}  // namespace vptyp