FROM gst-plugins-rs-base AS base

WORKDIR /workspace

RUN apt-get update && \
    apt-get install -y \
    meson \
    ninja-build \
    gcc \
    g++ \
    libnice-dev \
    gstreamer1.0-nice    

RUN apt-get update && \
    apt-get install -y \
    python3 \
    python3-venv 

RUN python3 -m venv ~/py && \
    . ~/py/bin/activate && \
    pip install conan

ENV DISPLAY=:0

CMD ["tail", "-F", "/dev/null"]
