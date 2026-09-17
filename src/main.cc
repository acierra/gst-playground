#include <gflags/gflags.h>
#include <glib.h>
#include <glog/logging.h>
#include <gst/gst.h>

#include <filesystem>

#include "basePlayer.hh"
#include "flags.hh"
#include "playerFactory.hh"

DEFINE_string(filename, "", "mp4 file path");
DEFINE_string(url, "", "web URL to stream from");
DEFINE_string(output, "", "output file path");
DEFINE_string(webrtc, "",
              "provide uri for signalling server. E.g.: ws://127.0.0.1:8443");

void loggerSetup(char* argv[]) {
  if (!std::filesystem::exists("logs") ||
      !std::filesystem::is_directory("logs")) {
    std::filesystem::create_directory("logs");
  }
  google::InitGoogleLogging(argv[0]);
  google::SetLogDestination(google::INFO, "logs/base.log");
  google::SetLogDestination(google::ERROR, "logs/base.log");
  google::LogToStderr();
}

int main(int argc, char* argv[]) {
  gflags::ParseCommandLineFlags(&argc, &argv, true);
  loggerSetup(argv);
  gst_init(&argc, &argv);

  GMainLoop* loop = g_main_loop_new(nullptr, false);

  vptyp::Flags flags{.url = FLAGS_url,
                     .filename = FLAGS_filename,
                     .output = FLAGS_output,
                     .wsUri = FLAGS_webrtc};

  vptyp::init_flags(flags);

  std::unique_ptr<vptyp::BasePlayer> player =
      vptyp::PlayerFactory().create(flags, *loop);

  if (!player) {
    LOG(FATAL) << "Player creation failed";
  }

  player->create();
  player->play();
  g_main_loop_run(loop);
  player->stop();

  g_main_loop_unref(loop);
  loop = nullptr;
  return 0;
}