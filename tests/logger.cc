#include "logger.hh"

#include <filesystem>

static bool loggerInitialised{false};

void loggerSetup(char* argv[]) {
  if (loggerInitialised) return;
  if (!std::filesystem::exists("logs") ||
      !std::filesystem::is_directory("logs")) {
    std::filesystem::create_directory("logs");
  }
  if (argv)
    google::InitGoogleLogging(argv[0]);
  else {
    google::InitGoogleLogging("Test");
  }
  google::SetLogDestination(google::INFO, "logs/base.log");
  google::SetLogDestination(google::ERROR, "logs/base.log");
  google::LogToStderr();
  loggerInitialised = true;
}