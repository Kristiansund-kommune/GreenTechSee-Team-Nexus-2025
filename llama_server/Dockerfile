FROM nvidia/cuda:12.6.2-devel-ubuntu24.04

# build deps + CURL for features the server enables by default
RUN apt-get update && apt-get install -y \
    git build-essential cmake libcurl4-openssl-dev \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /src
RUN git clone --depth 1 https://github.com/ggerganov/llama.cpp .

# Make the CUDA driver stub visible at link time
# (the real libcuda.so.1 will be injected at runtime by the NVIDIA toolkit)
RUN ln -s /usr/local/cuda/lib64/stubs/libcuda.so /usr/local/cuda/lib64/libcuda.so

# Build ONLY the server, with CUDA; target your 2080 SUPER (SM 75)
RUN cmake -S . -B build \
    -DGGML_CUDA=ON \
    -DCMAKE_CUDA_ARCHITECTURES=75 \
    -DLLAMA_BUILD_SERVER=ON \
    -DLLAMA_BUILD_EXAMPLES=OFF \
    -DLLAMA_BUILD_TESTS=OFF \
    -DLLAMA_BUILD_BENCHMARK=OFF \
 && cmake --build build -j

# Ship the server binary
RUN mkdir -p /app && cp build/bin/server /app/server
WORKDIR /app

EXPOSE 8080
CMD ["/app/server", "--help"]
