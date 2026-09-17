#include <gtest/gtest.h>

#include <element.hh>
#include <pipeline.hh>

#include "logger.hh"

class PipelineTest : public ::testing::Test {
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

 public:
  GMainLoop* loop{nullptr};
};

TEST_F(PipelineTest, PipelineCreation) {
  vptyp::Pipeline pipeline(*loop, "test-pipeline");
}

TEST_F(PipelineTest, AddElementByValue) {
  vptyp::Pipeline pipeline(*loop, "test-pipeline");

  vptyp::Element element("videotestsrc", "src");
  EXPECT_TRUE(element.is_initialised());

  EXPECT_NO_THROW(pipeline.add_element(std::move(element)));
}

TEST_F(PipelineTest, AddElementByReference) {
  vptyp::Pipeline pipeline(*loop, "test-pipeline");

  vptyp::Element element("videotestsrc", "src");
  EXPECT_TRUE(element.is_initialised());

  pipeline.add_element(element);
}

TEST_F(PipelineTest, PipelinePlaybackControl) {
  vptyp::Pipeline pipeline(*loop, "test-pipeline");

  vptyp::Element src("videotestsrc", "src");
  vptyp::Element sink("autovideosink", "sink");

  pipeline.add_element(src);
  pipeline.add_element(sink);
  EXPECT_TRUE(src.link(sink)) << "Failed to link elements";

  pipeline.play();
  usleep(250000);
  pipeline.stop();
}