#pragma once
#include <element.hh>

#include "glib.h"

namespace test {

class Element final : public vptyp::Element {
 public:
  Element(GMainLoop* loop, std::string_view element_name,
          std::string_view alias);
  GstElement* get();

 protected:
  bool on_pad_added(GstElement* src, GstPad* new_pad,
                    GstElement* target) override;

 private:
  GMainLoop* loop{nullptr};
};

}  // namespace test