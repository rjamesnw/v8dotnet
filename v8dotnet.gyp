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
                  'cflags':[  
                     '-Werror -Wall -std=c++11 -w -fpermissive -fPIC -c',
                  ],
                  'ldflags':[  
                     '-Wall -std=c++11 -shared -fPIC',
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
                        '-Wl,-rpath,. -L. -L../',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/testing/libgmock.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/testing/libgtest.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_base.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_libbase.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_libplatform.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_nosnapshot.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/obj.target/tools/gyp/libv8_snapshot.a',
                        '-lpthread -lstdc++ -lv8 -licui18n -licuuc -lglib-2.0 -lrt'
                     ]
                  },
               }
            ],
            ['OS=="mac"',
            {
               'xcode_settings': {
                     'OTHER_CPLUSPLUSFLAGS' : ['-w -std=c++11 -stdlib=libstdc+ +-Wc++11-extensions '],
                     'OTHER_LDFLAGS': ['-Wall -w  -shared '],
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
                        '-Wl,-rpath,. -L. -L../',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgmock.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgmock_main.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgtest.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libgtest_main.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_base.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_libbase.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_libplatform.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_nosnapshot.a',
                        '<(base_dir)/Source/V8.NET-Proxy/V8/out/<(target_arch).<(build_option)/libv8_snapshot.a',
                        '-lv8 -licui18n -licuuc '
                     ]
                  },
               }
            ]            
         ]
      } 
   ]
}