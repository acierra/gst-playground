script_folder="/workspace/docker-build"
echo "echo Restoring environment" > "$script_folder/deactivate_conanrunenv-release-x86_64.sh"
for v in LD_LIBRARY_PATH DYLD_LIBRARY_PATH PATH
do
   is_defined="true"
   value=$(printenv $v) || is_defined="" || true
   if [ -n "$value" ] || [ -n "$is_defined" ]
   then
       echo export "$v='$value'" >> "$script_folder/deactivate_conanrunenv-release-x86_64.sh"
   else
       echo unset $v >> "$script_folder/deactivate_conanrunenv-release-x86_64.sh"
   fi
done

export LD_LIBRARY_PATH="/root/.conan2/p/glog7aada7e0562d3/p/lib:/root/.conan2/p/gflaga10a3b9db4758/p/lib:/root/.conan2/p/libunf5a5badfaf79e/p/lib:/root/.conan2/p/xz_utc54e905cffd61/p/lib:/root/.conan2/p/zlib9e81e3baaa894/p/lib:/root/.conan2/p/b/gtest528f2d88fc336/p/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export DYLD_LIBRARY_PATH="/root/.conan2/p/glog7aada7e0562d3/p/lib:/root/.conan2/p/gflaga10a3b9db4758/p/lib:/root/.conan2/p/libunf5a5badfaf79e/p/lib:/root/.conan2/p/xz_utc54e905cffd61/p/lib:/root/.conan2/p/zlib9e81e3baaa894/p/lib:/root/.conan2/p/b/gtest528f2d88fc336/p/lib${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}"
export PATH="/root/.conan2/p/gflaga10a3b9db4758/p/bin:/root/.conan2/p/xz_utc54e905cffd61/p/bin${PATH:+:$PATH}"