#pragma once
#include <gst/gst.h>

#include <memory>

template <typename T>
struct Deleter {
  void operator()(T* element) {
    if (element) {
      gst_object_unref(element);
    }
  }
};

template <typename T>
std::unique_ptr<T, Deleter<T>> make_gst(T* obj) {
  return {obj, Deleter<T>()};
}