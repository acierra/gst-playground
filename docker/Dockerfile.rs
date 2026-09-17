FROM ubuntu:24.04 AS builder

WORKDIR /gst-plugins-rs

RUN apt update && \
    apt install -y \
    libglvnd0 \
    libgl1 \
    libglx0 \
    libgles2 \
    libegl1 \
    libxcb1 \
    libx11-xcb1 \
    build-essential \
    clang \
    libxkbcommon-x11-0 \
    gstreamer1.0-tools \
    gstreamer1.0-plugins-base \
    gstreamer1.0-plugins-good \
    gstreamer1.0-plugins-bad \
    gstreamer1.0-plugins-ugly \
    gstreamer1.0-libav \
    libgstreamer1.0-dev \
    libgstreamer-plugins-base1.0-dev && \
    rm -rf /var/lib/apt/lists/*

RUN apt update && \
    apt install -y \
        build-essential \
        git \
        pkg-config \
        python3 

RUN apt update && apt install -y rustup

RUN rustup default stable

RUN apt update && apt install -y libssl-dev

RUN cargo install cargo-c

RUN git clone https://gitlab.freedesktop.org/gstreamer/gst-plugins-rs.git .

# 0.12 is a release version relative to 1.24.x gstreamer
RUN git checkout 0.12

RUN apt update && apt install -y libcairo2-dev libpango1.0-dev

RUN apt update && apt install -y libgstreamer-plugins-bad1.0-dev

RUN RUSTFLAGS="-A dangerous_implicit_autorefs" cargo cinstall --prefix=/usr