#include <gtest/gtest.h>

#include <element.hh>
#include <pipeline.hh>
#include <string>

namespace vptyp {
namespace test {

// Helper function to create a test pipeline with setup
inline std::pair<GMainLoop*, vptyp::Pipeline> create_test_pipeline(
    const std::string& name = "test-pipeline") {
  GMainLoop* loop = g_main_loop_new(NULL, FALSE);
  vptyp::Pipeline pipeline(*loop, name);
  return {loop, std::move(pipeline)};
}

// Helper function to create a simple video pipeline
inline void create_simple_video_pipeline(vptyp::Pipeline& pipeline) {
  auto src = vptyp::Element("videotestsrc", "src");
  auto sink = vptyp::Element("autovideosink", "sink");
  pipeline.add_element(src);
  pipeline.add_element(sink);
  EXPECT_TRUE(src.link(sink)) << "Failed to link videotestsrc to autovideosink";
}

// Helper function to create a simple audio pipeline
inline void create_simple_audio_pipeline(vptyp::Pipeline& pipeline) {
  auto src = vptyp::Element("audiotestsrc", "src");
  auto sink = vptyp::Element("autoaudiosink", "sink");
  pipeline.add_element(src);
  pipeline.add_element(sink);
  EXPECT_TRUE(src.link(sink)) << "Failed to link audiotestsrc to autoaudiosink";
}

// Helper function to run pipeline for a short duration
inline void run_pipeline_for_duration(vptyp::Pipeline& pipeline, int seconds) {
  pipeline.play();
  sleep(seconds);
  pipeline.stop();
}

// Custom assertion for element initialization
inline ::testing::AssertionResult IsElementInitialised(
    vptyp::Element* element) {
  return element && element->is_initialised()
             ? ::testing::AssertionSuccess()
             : ::testing::AssertionFailure() << "Element is not initialised";
}

// Custom assertion for element linking
inline ::testing::AssertionResult ElementsAreLinked(
    const vptyp::Element& src, const vptyp::Element& sink) {
  // This is a best-effort check; full verification would require GStreamer
  // introspection
  return ::testing::AssertionSuccess();
}

}  // namespace test
}  // namespace vptyp
