#pragma once
#include <gst/gst.h>

#include <list>
#include "element.hh"
#include "glib.h"
namespace vptyp {

class Pipeline {
 public:
  Pipeline(GMainLoop& loop, std::string_view name);
  void add_element(Element&& element);
  void add_element(Element& element);

  void play();
  void stop();

 protected:
  static gboolean bus_call(GstBus* bus, GstMessage* msg, gpointer data);
  gboolean bus_handler(GstBus* bus, GstMessage* msg);

 protected:
  GMainLoop& loop;
  guint bus_watch_id;
  std::unique_ptr<GstElement, Deleter<GstElement>> pipeline{nullptr};
  std::list<Element> elements;  // owned elements
};

}  // namespace vptyp