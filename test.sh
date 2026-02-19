#!/bin/bash

set -e

cd "$(dirname "$0")"

echo "Running Catalyst tests for all languages..."
echo "=========================================="

echo ""
echo "Testing C#..."
dotnet run -- --Language=cs --BaseInputDir=./TestData --BaseOutputDir=./output/cs > /dev/null 2>&1
echo "C# - OK"

echo ""
echo "Testing TypeScript..."
dotnet run -- --Language=ts --BaseInputDir=./TestData --BaseOutputDir=./output/ts > /dev/null 2>&1
echo "TypeScript - OK"

echo ""
echo "Testing Unreal..."
dotnet run -- --Language=unreal --BaseInputDir=./TestData --BaseOutputDir=./output/unreal > /dev/null 2>&1
echo "Unreal - OK"

echo ""
echo "=========================================="
echo "All tests passed!"
