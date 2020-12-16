/*
 * Copyright 2020 Google LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#pragma once

namespace YetiVSI {
namespace DebugEngine {

// A managed C++ class that has similar behaviour to a std::unique_ptr.  It owns
// a pointer and makes sure the object is disposed when the ManagedUniquePtr
// goes out of scope.
template <typename T>
private ref class ManagedUniquePtr sealed {
 public:
  ManagedUniquePtr() : native_ptr_(nullptr){};
  explicit ManagedUniquePtr(T* native_ptr) : native_ptr_(native_ptr) {}
  template <typename T2>
  ManagedUniquePtr(ManagedUniquePtr<T2> % ptr) : native_ptr_(ptr.release()) {}

  // Destructor calls into the finalizer to clean up unmanaged resources.
  ~ManagedUniquePtr() { ManagedUniquePtr<T>::!ManagedUniquePtr(); }

  // Finalizer is meant to clean up unmanaged resources.  This will be called by
  // the GC if the class isn't explicitly deleted.
  !ManagedUniquePtr() { delete native_ptr_; }

  // Override the '=' operator, this will transfer ownership of the managed
  // object from one ManagedUniquePtr to another.
  template <typename T2>
      ManagedUniquePtr<T> % operator=(ManagedUniquePtr<T2> % ptr) {
    Reset(ptr.release());
    return *this;
  }

  // Override '->' operator to return a pointer to the managed object.
  T* operator->() { return native_ptr_; }

  // Override '*' operator to return a pointer to the managed object.
  static T& operator*(ManagedUniquePtr<T> % ptr) { return *(ptr.Get()); }

  // Get the underlying pointer to the object being managed.
  T* Get() { return native_ptr_; }

  // Release the managed object (ie. it will no longer be deleted when
  // ManagedUniquePtr comes out of scope).
  T* Release() {
    T* native_ptr = native_ptr_;
    native_ptr_ = nullptr;
    return native_ptr;
  }

  // Delete the managed object.
  void Reset() { Reset(nullptr); }

  // Replaces the managed object, ensure the old managed object is deleted.
  void Reset(T* native_ptr) {
    delete native_ptr_;
    native_ptr_ = native_ptr;
  }

 private:
  T* native_ptr_;
};

template <typename T, typename... Args>
ManagedUniquePtr<T> ^ MakeUniquePtr(Args&&... args) {
    return gcnew ManagedUniquePtr<T>(new T(std::forward<Args>(args)...));
}

}  // namespace DebugEngine
}  // namespace YetiVSI