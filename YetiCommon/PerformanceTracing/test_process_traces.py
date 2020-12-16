# Copyright 2020 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import unittest
import process_traces as sut


class TestProcessTraces(unittest.TestCase):

  def test_is_json(self):
    self.assertTrue(sut.is_json("{}"))
    self.assertTrue(sut.is_json("{ }"))
    self.assertFalse(sut.is_json("some log"))
    self.assertFalse(sut.is_json(""))
    self.assertTrue(sut.is_json("{\"ph\": \"X\"}"))
    self.assertTrue(sut.is_json("{\"key\": 5}"))

  def test_parse_json_events(self):
    events = ["{}", "some log", "{\"ph\": \"X\"}"]
    filtered = sut.parse_json_events(events)
    self.assertEqual(2, len(filtered))
    self.assertEqual({}, filtered[0])
    self.assertEqual("X", filtered[1]["ph"])

  def test_process_events(self):
    events = [
        "{\"tid\": 1, \"name\": \"myevent\"}",
        "{\"tid\": 2, \"name\": \"myevent\"}",
        "{\"tid\": 3, \"name\": \"namespace.WaitForEvent\"}"]
    parsed = sut.parse_json_events(events)
    processed = sut.process_events(parsed)
    self.assertEqual(parsed[0], processed[0])
    self.assertEqual(1, processed[1]["tid"])
    self.assertEqual(2, processed[2]["tid"])

if __name__ == "__main__":
  unittest.main()
