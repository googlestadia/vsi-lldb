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

"""Utility that converts logs from VSI Trace Logger to Chrome Tracing json.

Takes a list of files and concatenates them into a single trace file.
Prints the formatted json file to stdout.
Throws away thread ID and uses it to group events during visualization.
"""
from __future__ import print_function

import argparse
import json


def is_json(string):
  try:
    json.loads(string)
  except ValueError:
    return False
  return True


def parse_json_events(events):
  return [json.loads(event) for event in events if is_json(event)]


def process_events(events):
  """Flattens all events onto two threads for clearer visualization."""
  for event in events:
    event['tid'] = 1
    if event['name'].endswith('WaitForEvent'):
      event['tid'] = 2
  return events


def main():
  parser = argparse.ArgumentParser(
      description='Converts logs from VSI Trace Logger to Chrome Tracing json.')
  parser.add_argument('files', metavar='file', type=str, nargs='+',
                      help='one or more trace logs to process')
  parser.add_argument('--collapse-threads', action='store_true',
                      help='collapse all threads except WaitForEvent')

  args = parser.parse_args()

  events = []
  for f in args.files:
    with open(f, 'r') as log_file:
      log = log_file.read()
      lines = log.splitlines()
      events += parse_json_events(lines)

  if args.collapse_threads:
    events = process_events(events)

  print(json.dumps(events))

if __name__ == '__main__':
  main()
