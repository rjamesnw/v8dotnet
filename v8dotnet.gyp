{  
      "includes": [

    ],
   'variables':{  
      'base_dir%':'<(base_dir)',
      'target_arch%':'x64',
      'build_option%':'release',

   },
   'targets':[  
      {  
         'target_name':'libV8_Net_Proxy',
         'type':'shared_library',
         'toolsets': [ 'target' ],
         'msvs_guid':'5ECEC9E5-8F23-47B6-93E0-C3B328B3BE65',
         'direct_dependent_settings':{  
            'include_dirs':[  
               'Source/V8.NET-Proxy/V8/'
            ],
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
            ['OS=="linux"',
               { 
                  'defines': ['_LINUX'],
                  'cflags':[  
                     '-Werror -Wall -std=c++11 -w -fpermissive -fPIC -c',
                  ],
                  'ldflags':[  
                     '-Wall',
                  ],
                  'copies':[  
                     {  
                        'destination':'<(PRODUCT_DIR)/../../',
                        'files':[  
                           'Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/lib.target/libv8.so',
                        ],
                     }
                  ],
                  'link_settings':{  
                     'libraries':[  
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/testing/libgmock.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/testing/libgmock_main.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/testing/libgtest.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/testing/libgtest_main.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_base.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_libbase.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_libplatform.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_nosnapshot.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_snapshot.a',              
                     ]
                  },
               }
            ],
            ['OS=="mac"',
            {
               'defines': ['_OSX'],
               'xcode_settings': {
                     'ARCHS': ['i386'],
                     'OTHER_CPLUSPLUSFLAGS' : ['-w -std=c++11  -Wc++11-extensions -fPIC -c '],
                     'OTHER_LDFLAGS': ['-Wall -w'],
               },

                  'copies':[  
                     {  
                        'destination':'<(PRODUCT_DIR)/../../',
                        'files':[  
                           'Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8.dylib',
                        ],
                     }
                  ],
                  'link_settings':{  
                     'libraries':[  
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgmock.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgmock_main.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgtest.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgtest_main.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_base.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_libbase.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_libplatform.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_nosnapshot.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_snapshot.a',
                     ]
                  },
               }
            ] ,
            ['OS=="win"',
            {
               'defines': ['_WIN'],
                  'copies':[  
                     {  
                        'destination':'<(PRODUCT_DIR)/../../',
                        'files':[  
                           'Source/V8.NET-Proxy/V8/build/v8.dll',
                        ],
                     }
                  ],
                  'link_settings':{  
                     'libraries':[  
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/mksnapshot.lib',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8.lib',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8_base.lib',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8_external_snapshot.lib',                        
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8_libbase.lib',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8_libplatform.lib',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8_nosnapshot.lib',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/build/Debug/lib/v8_snapshot.lib',
                     ]
                  },
               }
            ],           
         ]
      } 
   ]
}
