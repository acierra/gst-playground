#include "playerFactory.hh"

#include <memory>

#include "src/basePlayer.hh"
#include "src/baseRtcPlayer.hh"
#include "src/webPlayer.hh"

namespace vptyp {

std::unique_ptr<BasePlayer> PlayerFactory::create(const Flags& flags,
                                                  GMainLoop& loop) {
  if (!flags.url.empty() && !flags.filename.empty()) {
    return std::make_unique<WebToFilePlayer>(loop, flags.url, flags.filename);
  }

  if (!flags.output.empty()) {
    return std::make_unique<VideoPlayback>(loop, flags.output);
  }

  if (!flags.wsUri.empty()) {
    return std::make_unique<BaseRTCPlayer>(loop, flags.wsUri);
  }

  return nullptr;
}

}  // namespace vptyp