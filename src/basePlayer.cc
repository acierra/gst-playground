#include "basePlayer.hh"

#include <glog/logging.h>
#include <gst/gst.h>

#include <format>

#include "element.hh"
namespace vptyp {
VideoPlayback::VideoPlayback(GMainLoop& loop, std::string_view file)
    : BasePlayer(), file(file), loop(loop), pipeline(loop, "video-playback") {}

void VideoPlayback::play() { pipeline.play(); }
void VideoPlayback::stop() { pipeline.stop(); }

void VideoPlayback::create() {
  LOG(INFO) << std::format("location: {}", file.data());

  Element filesrc = Element("filesrc", "filesrc");
  filesrc.object_set("location", file.data());
  pipeline.add_element(filesrc);
  auto decodeBin = Element("decodebin", "decodebin");
  pipeline.add_element(decodeBin);
  auto converter = Element("videoconvert", "converter");
  pipeline.add_element(converter);
  auto videosink = Element("autovideosink", "video-output");
  pipeline.add_element(videosink);

  std::list<Element> elements;
  elements.push_back(std::move(decodeBin));
  elements.push_back(std::move(converter));
  elements.push_back(std::move(videosink));
  auto res = filesrc.link(elements.begin(), elements.end());

  if (!res) {
    LOG(ERROR) << std::format("Linkage failed");
  }
}

}  // namespace vptyp