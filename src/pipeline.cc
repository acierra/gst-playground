#include "pipeline.hh"

#include <glog/logging.h>

#include <format>
#include <functional>

#include "glib.h"
#include "gst/gstmessage.h"
namespace vptyp {

gboolean Pipeline::bus_handler(GstBus* bus, GstMessage* msg) {
switch (GST_MESSAGE_TYPE(msg)) {
    case GST_MESSAGE_EOS: {
      g_print("End of stream\n");
      g_main_loop_quit(&loop);
      return false;
    }
    case GST_MESSAGE_ERROR: {
      gchar* debug;
      GError* error;

      gst_message_parse_error(msg, &error, &debug);
      g_free(debug);

      LOG(ERROR) << std::format("Error: {}", error->message);
      g_error_free(error);

      g_main_loop_quit(&loop);
      return false;
    }
    case GST_MESSAGE_INFO: {
      GError* err;
      gchar* debug;
      gst_message_parse_info(msg, &err, &debug);
      LOG(INFO) << std::format("err: {}, debug: {}", err->message, debug);
    }
    default:
      LOG(INFO) << std::format("Received message of type {}", static_cast<int>(GST_MESSAGE_TYPE(msg)));
      break;
  }
  return true;
}

gboolean Pipeline::bus_call(GstBus* bus, GstMessage* msg, gpointer data) {
  auto that = static_cast<Pipeline*>(data);
  return that->bus_handler(bus, msg);
}

void Pipeline::play() {
  gst_element_set_state(pipeline.get(), GST_STATE_PLAYING);
}

void Pipeline::stop() { gst_element_set_state(pipeline.get(), GST_STATE_NULL); }

Pipeline::Pipeline(GMainLoop& loop, std::string_view name) : loop(loop) {
  using namespace std::placeholders;
  pipeline = make_gst(gst_pipeline_new(name.data()));

  auto gstBus = make_gst(gst_pipeline_get_bus(GST_PIPELINE(pipeline.get())));
  
  bus_watch_id = gst_bus_add_watch(gstBus.get(), bus_call, this);
}

void Pipeline::add_element(Element&& element) {
  gst_bin_add(reinterpret_cast<GstBin*>(pipeline.get()), element.element.get());
  element.owned = 1;
}

void Pipeline::add_element(Element& element) {
  gst_bin_add(reinterpret_cast<GstBin*>(pipeline.get()), element.element.get());
  element.owned = 1;
}

}  // namespace vptyp