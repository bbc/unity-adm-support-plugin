#pragma once

#include <map>
#include <optional>

#define TO_RAD 3.14159265359/180.0

template<typename Key, typename Value>
std::optional<Value> getFromMap(std::map<Key, Value> &targetMap, Key key){
    auto it = targetMap.find(key);
    if(it == targetMap.end()) return std::optional<Value>();
    return std::optional<Value>(it->second);
}

template<typename Key, typename Value>
Value* getValuePointerFromMap(std::map<Key, Value> &targetMap, Key key){
    auto it = targetMap.find(key);
    if(it == targetMap.end()) return nullptr;
    return &(it->second);
}

template<typename Key, typename Value>
void setInMap(std::map<Key, Value> &targetMap, Key key, Value value){
    auto it = targetMap.find(key);
    if(it == targetMap.end()){
        targetMap.insert(std::make_pair(key, value));
    } else {
        it->second = value;
    }
}
