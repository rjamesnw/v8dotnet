{  
   'targets':[  
      {  
        'variables' : {
            'base_dir%':'Niull'
          },
         'target_name':'libV8_Net_Proxy',
         'type':'shared_library',
         'msvs_guid':'5ECEC9E5-8F23-47B6-93E0-C3B328B3BE65',
         'defines':[  
            'DEFINE_FOO',
            'DEFINE_A_VALUE=value',
         ],
        'direct_dependent_settings': {
          'include_dirs': ['Source/V8.NET-Proxy/V8/'],  # dependents need to find cruncher.h.
        },
         'include_dirs':[  
            'Source/V8.NET-Proxy/V8/',
         ],
         'sources':[  
            'Source/V8.NET-Proxy/Exports.cpp',
            'Source/V8.NET-Proxy/FunctionTemplateProxy.cpp',
            'Source/V8.NET-Proxy/HandleProxy.cpp',
            'Source/V8.NET-Proxy/ObjectTemplateProxy.cpp',
            'Source/V8.NET-Proxy/Utilities.cpp',
            'Source/V8.NET-Proxy/V8EngineProxy.cpp',
            'Source/V8.NET-Proxy/ValueProxy.cpp',

         ],
         'conditions':[  
            [  
               'OS=="linux"',
               {  
                  'defines':[  
                     'LINUX_DEFINE',

                  ],
                  'cflags':[  
                     '-Werror',
                     '-fPIC',
                     '-Wall',
                     '-std=c++11',
                     '-w',
                     '-fpermissive',
                     '-fPIC',
                     '-c',

                  ],
                  'ldflags':[  
                    '-Wall',
                    '-std=c++11',
                    '-shared',
                    '-fPIC',
                    '-Wl,-rpath,. -L. -L../ -lv8'
                  ],
                  'copies': [{
                    'destination': '<(PRODUCT_DIR)/../../',
                    'files': [
                        'Source/V8.NET-Proxy/V8/out/x64.release/lib.target/libicui18n.so',
                        'Source/V8.NET-Proxy/V8/out/x64.release/lib.target/libicuuc.so',
                        'Source/V8.NET-Proxy/V8/out/x64.release/lib.target/libv8.so',
                    ],
                  }],
                  'link_settings': {
                    'libraries': [
                       '/Build/v8dotnet/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/testing/libgmock.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/testing/libgtest.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/third_party/icu/libicudata.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/tools/gyp/libv8_base.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/tools/gyp/libv8_libbase.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/tools/gyp/libv8_libplatform.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/tools/gyp/libv8_nosnapshot.a',
                       '<(base_dir)/Source/V8.NET-Proxy/V8/out/x64.release/obj.target/tools/gyp/libv8_snapshot.a',
                       '-lpthread',
                       '-lstdc++',
                       '-licui18n',
                       '-licuuc',
                       '-lglib-2.0',
                       '-lrt',
                       '-Wl,-rpath,. -L. -L../ -lv8',
                       '-Wl,--verbose'
                      ]
                  },
                  'include_dirs':[  
                     '/usr/include/glib-2.0/',
                     '/usr/lib/x86_64-linux-gnu/glib-2.0/include/'
                  ],

               },
               [  
                  'OS=="win"',
                  {  
                     'defines':[  
                        'WINDOWS_SPECIFIC_DEFINE',

                     ]
                  }
               ]
            ]
         ]
      }
   ]
}