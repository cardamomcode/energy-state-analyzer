import os
import sys
import json
import datetime
import requests
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from typing import List, Dict, Any, Optional
from dataclasses import dataclass
from pathlib import Path

# Parameter explosion example
def complex_api_call(url, method, headers, params, data, timeout, retries, auth_token, verify_ssl, follow_redirects, cache_enabled):
    """This function has way too many parameters!"""
    if retries > 5:
        for i in range(retries):
            if method == "POST":
                if verify_ssl:
                    if follow_redirects:
                        if cache_enabled:
                            # Deep nesting example
                            response = requests.post(url, headers=headers, data=data, timeout=30, verify=True)
                            return response
    return None

# Magic numbers and strings everywhere
def process_data(data):
    """Full of magic values that should be constants"""
    if len(data) > 42:  # Magic number!
        filtered = [x for x in data if x > 3.14159]  # Another magic number
        if len(filtered) > 7:  # Yet another
            print("Error: Invalid data format detected")  # Magic string
            print("Please contact support at error-code-1337")  # Magic string with number
    
    return data[:100]  # Magic slice

# Utils sprawl - this file is doing too much!
def format_date(date):
    return date.strftime("%Y-%m-%d")

def validate_email(email):
    return "@" in email

def calculate_tax(amount):
    return amount * 0.25  # Magic number again!

def hash_password(password):
    return hash(password)

def send_notification(message):
    print(f"Notification: {message}")

def parse_json(data):
    return json.loads(data)

def generate_uuid():
    import uuid
    return str(uuid.uuid4())

def compress_data(data):
    import gzip
    return gzip.compress(data.encode())

def log_error(error):
    print(f"ERROR: {error}")

def create_backup():
    pass

def cleanup_temp_files():
    pass

# This function has both high complexity AND parameter explosion
def mega_function(a, b, c, d, e, f, g, h, i, j):
    """The ultimate energy violation"""
    if a > 10:
        for item in b:
            if item.status == "active":
                for subitem in item.children:
                    if subitem.valid:
                        for detail in subitem.metadata:
                            if detail.priority == "high":
                                for rule in detail.rules:
                                    if rule.enabled:
                                        # 6 levels deep!
                                        process_rule(rule, c, d, e, f, g, h, i, j)

def process_rule(rule, *args):
    """At least this one is extracted"""
    pass