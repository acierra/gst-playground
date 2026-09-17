#include <gtest/gtest.h>

#include <element.hh>
#include <pipeline.hh>

#include "logger.hh"

class IntegrationTest : public ::testing::Test {
 protected:
  void SetUp() override {
    loggerSetup(nullptr);
    gst_init(nullptr, nullptr);
    loop = g_main_loop_new(nullptr, false);
  }
  void TearDown() override {
    g_main_loop_unref(loop);
    loop = nullptr;
  }

 private:
  GMainLoop* loop{nullptr};
};

TEST_F(IntegrationTest, SimpleVideoPipeline) {
  GMainLoop* loop = g_main_loop_new(NULL, FALSE);
  vptyp::Pipeline pipeline(*loop, "video-test");

  vptyp::Element src("videotestsrc", "src");
  vptyp::Element sink("autovideosink", "sink");

  pipeline.add_element(src);
  pipeline.add_element(sink);
  EXPECT_TRUE(src.link(sink)) << "Failed to link videotestsrc to autovideosink";

  // Run pipeline briefly
  pipeline.play();
  sleep(1);  // Run for 1 second
  pipeline.stop();

  g_main_loop_unref(loop);
}

TEST_F(IntegrationTest, SimpleAudioPipeline) {
  GMainLoop* loop = g_main_loop_new(NULL, FALSE);
  vptyp::Pipeline pipeline(*loop, "audio-test");

  vptyp::Element src("audiotestsrc", "src");
  vptyp::Element sink("autoaudiosink", "sink");

  pipeline.add_element(src);
  pipeline.add_element(sink);
  EXPECT_TRUE(src.link(sink)) << "Failed to link audiotestsrc to autoaudiosink";

  // Run pipeline briefly
  pipeline.play();
  sleep(1);  // Run for 1 second
  pipeline.stop();

  g_main_loop_unref(loop);
}
