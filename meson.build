project('gstPlayground', 'cpp',
    default_options : ['cpp_std=c++20']
)

# RUNPATH in combination with conan and transitive deps working strange
add_project_link_arguments('-Wl,--disable-new-dtags', language: 'cpp')

# Transitive dep on gflags when not passed by conan isn't correctly linked
# it's possible to fix by directly showing dep on gflags, but... v
add_project_link_arguments('-Wl,--copy-dt-needed-entries', language: 'cpp')

glib_dep = dependency('glib-2.0', fallback: 'glib')
gst_dep = dependency('gstreamer-1.0', fallback: 'gstreamer')
glog_dep = dependency('libglog', required: true)

libsrc = [
    'src/element.cc',
    'src/basePlayer.cc',
    'src/webPlayer.cc',
    'src/pipeline.cc',
    'src/baseRtcPlayer.cc',
]

deps = [
    glib_dep,
    gst_dep,
    glog_dep]

gstpp = library('gstpp',
    sources: libsrc,
    dependencies: deps,
)

gstpp_dep = declare_dependency(
    link_with: gstpp,
    include_directories: 'src/',
    dependencies: deps)

src = [
    'src/playerFactory.cc',
    'src/flags.cc',
    'src/main.cc',
]

exec = executable('gstPlayground',
  sources: src,
  dependencies: gstpp_dep,
)

subdir('tests')