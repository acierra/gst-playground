#include "baseRtcPlayer.hh"

#include <glog/logging.h>

#include "src/pipeline.hh"

namespace vptyp {

bool validate_ws(const std::string& wsUri) { return true; }

BaseRTCPlayer::BaseRTCPlayer(GMainLoop& loop, const std::string& wsUri)
    : BasePlayer(), wsUri(wsUri), loop(loop), pipeline(loop, "BaseRTCPlayer") {}

void BaseRTCPlayer::create() {
  auto src = Element("videotestsrc", "src");
  auto rssink = Element("webrtcsink", "sink");

  if (!rssink.is_initialised()) {
    LOG(FATAL) << "Failed on init of webrtcsink, make sure, that "
                  "gst-plugins-rs is installed on the system";
  }

  if (!validate_ws(wsUri)) {
    LOG(FATAL) << "Web Socket uri is incorrect, please, verify";
  }

  rssink.child_proxy_set("signaller::uri", wsUri.c_str());
  rssink.object_set("congestion-control", 0);
  src.object_set("pattern", 18);

  pipeline.add_element(src);
  pipeline.add_element(rssink);
  src.link(rssink);
}

void BaseRTCPlayer::play() { pipeline.play(); }

void BaseRTCPlayer::stop() { pipeline.stop(); }

}  // namespace vptyp