#include "webPlayer.hh"

#include <gst/gst.h>

#include "element.hh"

namespace vptyp {
WebToFilePlayer::WebToFilePlayer(GMainLoop& loop, std::string_view url,
                                 std::string_view output_file)
    : BasePlayer(),
      url(url),
      output_file(output_file),
      loop(loop),
      pipeline(loop, "web-to-file-player") {}

void WebToFilePlayer::play() { pipeline.play(); }
void WebToFilePlayer::stop() { pipeline.stop(); }

void WebToFilePlayer::create() {
  Element src = Element("souphttpsrc", "web-source");
  src.object_set("location", url.data());
  pipeline.add_element(src);

  auto demux = Element("qtdemux", "demuxer");
  pipeline.add_element(demux);

  auto parse = Element("h264parse", "h264parse");
  pipeline.add_element(parse);

  auto decoder = Element("avdec_h264", "decoder");
  pipeline.add_element(decoder);

  auto converter = Element("videoconvert", "converter");
  pipeline.add_element(converter);

  auto encoder = Element("x264enc", "encoder");
  pipeline.add_element(encoder);

  auto muxer = Element("mp4mux", "muxer");
  pipeline.add_element(muxer);

  Element sink = Element("filesink", "file-sink");
  sink.object_set("location", output_file.data());
  pipeline.add_element(sink);

  std::list<Element> elements;
  elements.push_back(std::move(demux));
  elements.push_back(std::move(parse));
  elements.push_back(std::move(decoder));
  elements.push_back(std::move(converter));
  elements.push_back(std::move(encoder));
  elements.push_back(std::move(muxer));
  elements.push_back(std::move(sink));

  src.link(elements.begin(), elements.end());
}

}  // namespace vptyp